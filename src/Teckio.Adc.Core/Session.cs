using System.Text;
namespace Teckio.Adc.Core;
public sealed record Measurement(RawSample Raw, double Seconds, double Value, bool Valid);
public sealed record SessionInfo(DateTimeOffset Started, bool Demo, string Device, uint Mode, uint Configuration, double Reference);
public sealed record Capture(SessionInfo Info, Measurement[] Samples, long Received, long IndexGaps, uint FirmwareDrops,
    long Invalid, long Evicted, string? RecordingPath);
public sealed class Session : IDisposable
{
    readonly object gate = new();
    readonly Measurement[] ring;
    int head, count; uint lastTick, lastIndex; ulong elapsed; bool first = true;
    long received, gaps, invalid, evicted; uint drops;
    StreamWriter? writer; string? recording;
    public SessionInfo Info { get; }
    readonly Ad7195Profile profile = new();
    public Session(SessionInfo info, int capacity = 262144) { Info = info; ring = new Measurement[capacity]; }
    public void StartRecording(string path) {
        lock (gate) {
            if (writer != null) throw new InvalidOperationException("Ya se está grabando.");
            writer = new(path, false, new UTF8Encoding(false), 65536); recording = path; writer.WriteLine(Header);
        }
    }
    public const string Header = "index,time_s,ticks,channel,raw_dec,raw_hex,status_hex,value,unit,valid";
    public static string Csv(Measurement s) => FormattableString.Invariant(
        $"{s.Raw.Index},{s.Seconds:R},{s.Raw.Ticks},{s.Raw.Channel},{s.Raw.Code},0x{s.Raw.Code:X6},0x{s.Raw.Status:X2},{s.Value:R},{(s.Raw.Channel == 2 ? "degC" : "V")},{s.Valid}");
    public void Add(RawSample raw, uint firmwareDrops) {
        lock (gate) {
            if (!first) { elapsed += unchecked(raw.Ticks - lastTick); gaps += Math.Max(0L, (long)unchecked(raw.Index - lastIndex) - 1); }
            first = false; lastTick = raw.Ticks; lastIndex = raw.Index; drops = firmwareDrops;
            bool valid = !raw.AdcError && !raw.NoReference && ((Info.Mode & 0x2000) == 0 || raw.ParityOk);
            if(raw.Channel == 2 && (Info.Configuration & 15) != 0) valid = false;
            double value = raw.Channel == 2 ? (raw.Code - 8388608.0) / 2815 - 273.15 : profile.Convert(raw.Code, Info.Configuration, Info.Reference);
            Measurement m = new(raw, elapsed / 3000000.0, value, valid);
            ring[head] = m; head = (head + 1) % ring.Length;
            if (count < ring.Length) count++; else evicted++;
            received++; if (!valid) invalid++;
            writer?.WriteLine(Csv(m));
        }
    }
    public Capture Snapshot(int max = int.MaxValue) {
        lock (gate) {
            int n = Math.Min(count, max); Measurement[] result = new Measurement[n];
            for (int i = 0; i < n; i++) result[i] = ring[(head - n + i + ring.Length) % ring.Length];
            return new(Info, result, received, gaps, drops, invalid, evicted, recording);
        }
    }
    public void FinalizeCounts(uint captured, uint firmwareDrops) {
        lock(gate) { drops = firmwareDrops; gaps = Math.Max(gaps, Math.Max(0L, (long)captured - received)); }
    }
    public void Dispose() { lock (gate) { writer?.Dispose(); writer = null; } }
}
