using System.Buffers.Binary;
namespace Teckio.Adc.Core;
public sealed record Packet(byte Opcode, ushort Sequence, byte[] Payload);
public static class Wire
{
    public const int FrameSize = 64;
    public static ushort Crc(ReadOnlySpan<byte> data)
    {
        ushort c = 0xffff;
        foreach (byte b in data) {
            c ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++) c = (ushort)((c & 0x8000) != 0 ? (c << 1) ^ 0x1021 : c << 1);
        }
        return c;
    }
    public static byte[] Encode(byte op, ushort seq, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > 54) throw new ArgumentOutOfRangeException(nameof(payload));
        byte[] f = new byte[64];
        f[0] = 0x54; f[1] = 0x4b; f[2] = 1; f[3] = op;
        BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(4), seq); f[6] = (byte)payload.Length;
        payload.CopyTo(f.AsSpan(8));
        BinaryPrimitives.WriteUInt16LittleEndian(f.AsSpan(62), Crc(f.AsSpan(0, 62)));
        return f;
    }
    public static uint U32(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt32LittleEndian(p);
    public static byte[] Words(params uint[] words)
    {
        byte[] p = new byte[words.Length * 4];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(i * 4), words[i]);
        return p;
    }
}
public sealed class PacketDecoder
{
    readonly List<byte> buffer = new(256);
    public long CorruptFrames { get; private set; }
    public IEnumerable<Packet> Feed(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes) buffer.Add(b);
        List<Packet> result = [];
        while (buffer.Count >= 2) {
            if (buffer[0] != 0x54 || buffer[1] != 0x4b) { buffer.RemoveAt(0); continue; }
            if (buffer.Count < 64) break;
            byte[] f = buffer.GetRange(0, 64).ToArray();
            if (f[2] != 1 || f[6] > 54 || Wire.Crc(f.AsSpan(0, 62)) != BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(62))) {
                CorruptFrames++; buffer.RemoveAt(0); continue;
            }
            result.Add(new(f[3], BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(4)), f.AsSpan(8, f[6]).ToArray()));
            buffer.RemoveRange(0, 64);
        }
        return result;
    }
}
public readonly record struct RawSample(uint Index, uint Ticks, uint Code, byte Status)
{
    public int Channel => Status & 7;
    public bool AdcError => (Status & 0x40) != 0;
    public bool NoReference => (Status & 0x20) != 0;
    public bool ParityOk => ((System.Numerics.BitOperations.PopCount(Code) & 1) != 0) == ((Status & 0x10) != 0);
    public static RawSample Read(ReadOnlySpan<byte> p) => new(Wire.U32(p), Wire.U32(p[4..]),
        (uint)(p[8] | p[9] << 8 | p[10] << 16), p[11]);
}
public interface IAdcProfile
{
    string Name { get; }
    string[] Channels { get; }
    double Convert(uint code, uint configuration, double reference);
    string Validate(uint mode, uint configuration);
}
