using System.Numerics;
namespace Teckio.Adc.Core;
public sealed record Statistics(int Count, double Mean, double Min, double Max, double Rms, double Sigma,
    double PeakToPeak, double NoiseFreeBits, double EffectiveResolution, double Rate, double IntervalJitterUs);
public sealed record Spectrum(double[] Frequency, double[] Dbfs, double PeakHz, int Length, string Note);
public static class Analysis
{
    public static Statistics Compute(double[] values, double[] times, double fullSpan)
    {
        if (values.Length == 0) return new(0, 0, 0, 0, 0, 0, 0, double.NaN, double.NaN, 0, 0);
        double mean = 0, m2 = 0, sumSquares = 0, min = double.MaxValue, max = double.MinValue;
        for (int i = 0; i < values.Length; i++) {
            double x = values[i], d = x - mean; mean += d / (i + 1); m2 += d * (x - mean);
            sumSquares += x * x; min = Math.Min(min, x); max = Math.Max(max, x);
        }
        double sigma = values.Length > 1 ? Math.Sqrt(m2 / (values.Length - 1)) : 0;
        double dt = times.Length > 1 ? (times[^1] - times[0]) / (times.Length - 1) : 0, variance = 0;
        for (int i = 1; i < times.Length; i++) variance += Math.Pow(times[i] - times[i - 1] - dt, 2);
        return new(values.Length, mean, min, max, Math.Sqrt(sumSquares / values.Length), sigma, max - min,
            max > min ? Math.Log2(fullSpan / (max - min)) : double.NaN,
            sigma > 0 ? Math.Log2(fullSpan / sigma) : double.NaN,
            dt > 0 ? 1 / dt : 0, times.Length > 1 ? Math.Sqrt(variance / (times.Length - 1)) * 1e6 : 0);
    }
    public static (double[] X, double[] Y) Histogram(double[] values, int bins = 64)
    {
        if (values.Length == 0) return ([], []);
        double lo = values.Min(), hi = values.Max();
        if (hi == lo) { lo -= 0.5e-9; hi += 0.5e-9; }
        double[] x = new double[bins], y = new double[bins];
        for (int i = 0; i < bins; i++) x[i] = lo + (i + 0.5) * (hi - lo) / bins;
        foreach (double v in values) y[Math.Clamp((int)((v - lo) / (hi - lo) * bins), 0, bins - 1)]++;
        return (x, y);
    }
    public static Spectrum Fft(double[] samples, double rate, double fullSpan)
    {
        int n = 1; while (n * 2 <= Math.Min(samples.Length, 8192)) n *= 2;
        if (n < 32 || rate <= 0 || fullSpan <= 0) return new([], [], 0, n, "Se necesitan ≥32 muestras uniformes del mismo canal.");
        Complex[] a = new Complex[n]; double mean = samples.TakeLast(n).Average(), windowSum = 0;
        for (int i = 0; i < n; i++) {
            double w = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            a[i] = (samples[samples.Length - n + i] - mean) * w; windowSum += w;
        }
        for (int i = 1, j = 0; i < n; i++) {
            int bit = n >> 1; for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit; if (i < j) (a[i], a[j]) = (a[j], a[i]);
        }
        for (int len = 2; len <= n; len <<= 1) {
            Complex step = Complex.FromPolarCoordinates(1, -2 * Math.PI / len);
            for (int i = 0; i < n; i += len) {
                Complex w = 1;
                for (int j = 0; j < len / 2; j++) {
                    Complex u = a[i + j], v = a[i + j + len / 2] * w;
                    a[i + j] = u + v; a[i + j + len / 2] = u - v; w *= step;
                }
            }
        }
        double[] freq = new double[n / 2], db = new double[n / 2]; int peak = 1;
        for (int i = 0; i < freq.Length; i++) {
            freq[i] = i * rate / n;
            db[i] = 20 * Math.Log10(Math.Max(1e-15, (2 * a[i].Magnitude / windowSum) / (fullSpan / 2)));
            if (i > 0 && db[i] > db[peak]) peak = i;
        }
        return new(freq, db, freq[peak], n, "Hann, DC eliminado, amplitud pico referida a medio rango; no PSD ni ENOB/SINAD certificado.");
    }
}
