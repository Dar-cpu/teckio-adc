using System.IO;
using Teckio.Adc.Core;
namespace Teckio.AdcLab.Services;
public sealed class DeviceClient : IDisposable
{
    IDeviceTransport? port;
    readonly Func<string, IDeviceTransport> createTransport;
    CancellationTokenSource? cancel;
    Task? reader;
    readonly SemaphoreSlim commands = new(1, 1);
    readonly object gate = new();
    TaskCompletionSource<Packet>? pending;
    ushort sequence, expectedSequence; byte expectedOpcode;
    PacketDecoder decoder = new();
    long receivedBytes;
    public DeviceClient() : this(name => new SerialTransport(name)) { }
    public DeviceClient(Func<string, IDeviceTransport> factory) => createTransport = factory;
    public long ReceivedBytes => Interlocked.Read(ref receivedBytes);
    public event Action<RawSample, uint>? Sample;
    public event Action<string>? Trace;
    public event Action<Exception>? Fault;
    public long CorruptFrames => decoder.CorruptFrames;
    public bool Connected => port?.IsOpen == true;
    public string Identity { get; private set; } = "Sin conexión";
    public async Task Connect(string name, int helloTimeoutMs = 1800)
    {
        Dispose(); decoder = new(); Interlocked.Exchange(ref receivedBytes, 0);
        try {
            port = createTransport(name);
            port.Open(); port.DiscardInBuffer(); cancel = new();
            var transport = port; var token = cancel.Token;
            reader = Task.Run(() => ReadLoop(transport, token));
            Trace?.Invoke($"ABIERTO {name} · 115200 8N1 · DTR=1 · sin control de flujo");
            // Allow Windows to finish SET_CONTROL_LINE_STATE after Open().
            await Task.Delay(150, token);
            byte[] h = [];
            for (int attempt = 1; attempt <= 3; attempt++) {
                try { h = await Command(1, timeoutMs: helloTimeoutMs); break; }
                catch (TimeoutException) when (attempt < 3) {
                    Trace?.Invoke($"HELLO {attempt}/3 sin respuesta; RX={ReceivedBytes} bytes, CRC inválidos={CorruptFrames}. Reintentando HELLO.");
                }
                catch (TimeoutException ex) {
                    throw new IOException($"{name} abrió, pero el PIC no respondió a HELLO tras 3 intentos. " +
                        $"RX={ReceivedBytes} bytes; CRC inválidos={CorruptFrames}. " +
                        (ReceivedBytes == 0 ? "Revisa que hayas programado el firmware 1.0.1 o posterior (corrección USB CDC/ZLP). " : "Revisa el firmware y la versión de protocolo. ") +
                        "El puerto se ha cerrado; puedes volver a conectar. Consulta la consola para ver el intercambio.", ex);
                }
            }
            if (h.Length != 11 || h[0] != 1 || Wire.U32(h.AsSpan(5)) != 3000000)
                throw new IOException("Protocolo/reloj no compatible con ADC Lab.");
            Identity = $"TECKIO PIC18K · FW {h[1]}.{h[2]}.{h[3]} · AD7195 ID 0x{h[4]:X2} · {name}";
            if (h[4] != 0xA6) Trace?.Invoke("ID distinto de 0xA6: revisar diagnóstico del ADC.");
            await Command(0x21); // recover interrupted previous host acquisition
        } catch (UnauthorizedAccessException ex) {
            Dispose(); throw new IOException($"No se pudo abrir {name}. Cierra PowerShell, terminales u otra instancia que esté usando este puerto y vuelve a conectar.", ex);
        } catch { Dispose(); throw; }
    }
    void ReadLoop(IDeviceTransport transport, CancellationToken token)
    {
        byte[] buffer = new byte[4096];
        try {
            while (!token.IsCancellationRequested) {
                int n;
                try { n = transport.Read(buffer, 0, buffer.Length); } catch (TimeoutException) { continue; }
                long before = Interlocked.Add(ref receivedBytes, n) - n;
                if (before < 128 && n > 0) Trace?.Invoke("USB RX " + Convert.ToHexString(buffer.AsSpan(0, Math.Min(n, (int)(128 - before)))));
                foreach (Packet packet in decoder.Feed(buffer.AsSpan(0, n))) {
                    if (packet.Opcode == 0x70 && packet.Payload.Length >= 16 && (packet.Payload.Length - 4) % 12 == 0) {
                        uint drop = Wire.U32(packet.Payload);
                        for (int i = 4; i < packet.Payload.Length; i += 12) Sample?.Invoke(RawSample.Read(packet.Payload.AsSpan(i)), drop);
                    } else {
                        lock (gate) {
                            if (packet.Sequence == expectedSequence && packet.Opcode == expectedOpcode) pending?.TrySetResult(packet);
                        }
                    }
                }
            }
        } catch (Exception e) {
            if (!token.IsCancellationRequested) {
                lock (gate) pending?.TrySetException(e);
                Fault?.Invoke(e);
            }
        }
    }
    public async Task<byte[]> Command(byte opcode, byte[]? payload = null, int timeoutMs = 2500, bool acceptReadbackMismatch = false)
    {
        await commands.WaitAsync();
        try {
            if (!Connected) throw new IOException("Conecta la TECKIO PIC18K.");
            TaskCompletionSource<Packet> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ushort seq = ++sequence;
            lock (gate) { pending = completion; expectedSequence = seq; expectedOpcode = (byte)(opcode | 0x80); }
            byte[] frame = Wire.Encode(opcode, seq, payload ?? []);
            Trace?.Invoke($"TX #{seq} 0x{opcode:X2} {Convert.ToHexString(payload ?? [])}");
            port!.Write(frame, 0, frame.Length);
            Packet response;
            try { response = await completion.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMs)); }
            catch (TimeoutException ex) {
                throw new TimeoutException($"Sin respuesta a 0x{opcode:X2}, secuencia {seq}, en {timeoutMs} ms. RX={ReceivedBytes} bytes; CRC inválidos={CorruptFrames}.", ex);
            }
            Trace?.Invoke($"RX #{seq} 0x{response.Opcode:X2} {Convert.ToHexString(response.Payload)}");
            if (response.Payload.Length == 0) throw new IOException("Respuesta vacía.");
            if (response.Payload[0] == 4 && acceptReadbackMismatch) {
                Trace?.Invoke("READBACK MISMATCH: " + Convert.ToHexString(response.Payload[1..]));
                return response.Payload[1..];
            }
            if (response.Payload[0] != 0) throw new IOException(response.Payload[0] switch {
                1 => "Comando no soportado.", 2 => "Parámetros no válidos.", 3 => "Detén la adquisición antes de cambiar registros.",
                4 => "El valor leído no coincide con lo escrito." + (response.Payload.Length==5?$" Lectura: 0x{Wire.U32(response.Payload.AsSpan(1)):X6}.":""), _ => $"Error dispositivo {response.Payload[0]}."
            });
            return response.Payload[1..];
        } finally { lock (gate) pending = null; commands.Release(); }
    }
    public async Task<uint> ReadRegister(byte reg) { byte[] p = await Command(0x10, [reg]); return Wire.U32(p.AsSpan(1)); }
    public Task<byte[]> WriteRegister(byte reg, uint value, bool verify = true) => Command(0x11, [reg, .. Wire.Words(value)], acceptReadbackMismatch: !verify);
    public void Dispose()
    {
        cancel?.Cancel();
        try { port?.Close(); } catch (IOException) { } catch (InvalidOperationException) { }
        try { reader?.Wait(500); } catch (AggregateException) { }
        port?.Dispose(); port = null; cancel?.Dispose(); cancel = null; reader = null;
        lock (gate) pending?.TrySetException(new IOException("Desconectado."));
        Identity = "Sin conexión";
    }
}
