using System.Collections.Concurrent;
using Teckio.Adc.Core;
using Teckio.AdcLab.Services;

static class ConnectionTests
{
    public static async Task Run(Action<bool, string> check)
    {
        var hello = Wire.Encode(1, 1, []);
        check(hello.Length == 64 && hello[62] == 0xF0 && hello[63] == 0x6F,
            "COM20 PowerShell HELLO regression vector");
        var serial = new FakeTransport(); int hellos = 0;
        serial.OnWrite = request => {
            if (request.Opcode == 1) {
                hellos++;
                if (hellos == 1) return; // lost first reply / late enumeration
                byte[] payload = [0, 1, 1, 0, 1, 0xA6, .. Wire.Words(3000000), 64, 4];
                serial.Enqueue(Wire.Encode(0x81, (ushort)(request.Sequence - 1), payload)); // stale reply
                byte[] response = Wire.Encode(0x81, request.Sequence, payload);
                serial.Enqueue(response[..17]); serial.Enqueue(response[17..]);
            } else serial.Enqueue(Wire.Encode((byte)(request.Opcode | 0x80), request.Sequence, [0]));
        };
        using (var client = new DeviceClient(_ => serial)) {
            await client.Connect("COM-test", 100);
            check(hellos == 2 && client.Identity.Contains("1.0.1") && client.Connected,
                "HELLO retry ignores stale sequence and accepts fragmented response");
            check(serial.Opcodes.SequenceEqual(new byte[] {1,1,0x21}), "connection ends with STOP recovery");
            serial.OnWrite = _ => { };
            try { await client.Command(0x11, [2, .. Wire.Words(0x140)], 50); check(false, "write timeout expected"); }
            catch (TimeoutException) { check(serial.Opcodes.Count(x => x == 0x11) == 1, "register writes are never retried"); }
        }
        check(serial.Disposed && !serial.IsOpen, "disconnect releases transport");
        var silent = new FakeTransport();
        using (var client = new DeviceClient(_ => silent)) {
            try { await client.Connect("COM-silent", 50); check(false, "HELLO timeout expected"); }
            catch (IOException ex) {
                check(ex.Message.Contains("RX=0") && ex.Message.Contains("3 intentos") &&
                    !client.Connected && silent.Disposed && silent.Opcodes.Count == 3,
                    "silent PIC reports RX diagnostics and releases COM after bounded retries");
            }
        }
        var locked = new FakeTransport { DenyOpen = true };
        using (var client = new DeviceClient(_ => locked)) {
            try { await client.Connect("COM-locked", 50); check(false, "open error expected"); }
            catch (IOException ex) { check(locked.Disposed && ex.Message.Contains("PowerShell"), "failed Open is disposed and explains port ownership"); }
        }
    }
    sealed class FakeTransport : IDeviceTransport
    {
        readonly BlockingCollection<byte[]> received = new();
        public List<byte> Opcodes { get; } = [];
        public Action<Packet>? OnWrite;
        public bool IsOpen { get; private set; }
        public bool Disposed { get; private set; }
        public bool DenyOpen;
        public void Open() { if (DenyOpen) throw new UnauthorizedAccessException(); IsOpen = true; }
        public void Close() { IsOpen = false; received.CompleteAdding(); }
        public void DiscardInBuffer() { }
        public void Enqueue(byte[] bytes) => received.Add(bytes);
        public int Read(byte[] buffer, int offset, int count) {
            if (!received.TryTake(out var bytes, 20)) throw new TimeoutException();
            Array.Copy(bytes, 0, buffer, offset, bytes.Length); return bytes.Length;
        }
        public void Write(byte[] buffer, int offset, int count) {
            var packet = new PacketDecoder().Feed(buffer.AsSpan(offset, count)).Single();
            Opcodes.Add(packet.Opcode); OnWrite?.Invoke(packet);
        }
        public void Dispose() { Close(); Disposed = true; }
    }
}
