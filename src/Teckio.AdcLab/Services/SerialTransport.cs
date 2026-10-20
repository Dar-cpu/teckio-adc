using System.IO.Ports;
namespace Teckio.AdcLab.Services;

// Small boundary for testing the real handshake/reader against a simulated
// byte stream without substituting the protocol implementation.
public interface IDeviceTransport : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void Close();
    void DiscardInBuffer();
    int Read(byte[] buffer, int offset, int count);
    void Write(byte[] buffer, int offset, int count);
}

public sealed class SerialTransport(string name) : IDeviceTransport
{
    readonly SerialPort port = new(name, 115200) {
        DtrEnable = true, RtsEnable = false, Handshake = Handshake.None,
        ReadTimeout = 200, WriteTimeout = 1000, ReadBufferSize = 65536
    };
    public bool IsOpen => port.IsOpen;
    public void Open() => port.Open();
    public void Close() => port.Close();
    public void DiscardInBuffer() => port.DiscardInBuffer();
    public int Read(byte[] buffer, int offset, int count) => port.Read(buffer, offset, count);
    public void Write(byte[] buffer, int offset, int count) => port.Write(buffer, offset, count);
    public void Dispose() => port.Dispose();
}
