using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace Teckio.AdcLab.Controls;
public sealed class PlotView : FrameworkElement
{
    double[] xs = [], ys = [];
    double zoom = 1;
    Point? cursor;
    public bool Bars { get; set; }
    public string XUnit { get; set; } = "s";
    public string YUnit { get; set; } = "V";
    public void Set(double[] x, double[] y) { xs = x; ys = y; InvalidateVisual(); }
    public void AutoScale() { zoom = 1; InvalidateVisual(); }
    public PlotView()
    {
        ClipToBounds = true;
        MouseWheel += (_, e) => { zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.5 : 1 / 1.5), 1, 64); InvalidateVisual(); };
        MouseMove += (_, e) => { cursor = e.GetPosition(this); InvalidateVisual(); };
        MouseLeave += (_, _) => { cursor = null; InvalidateVisual(); };
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) AutoScale(); };
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var text = (Brush)FindResource("Muted"); var grid = new Pen((Brush)FindResource("Line"), 0.6);
        var accent = (Brush)FindResource("Accent");
        double left = 76, top = 18, width = Math.Max(1, ActualWidth - 94), height = Math.Max(1, ActualHeight - 52);
        dc.DrawRectangle(Brushes.Transparent, null, new(0, 0, ActualWidth, ActualHeight));
        void Label(string s, double x, double y, Brush? brush = null)
        {
            var f = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush ?? text, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(f, new(x, y));
        }
        int total = Math.Min(xs.Length, ys.Length), n = (int)(total / zoom), start = Math.Max(0, total - Math.Max(2, n));
        var valid = Enumerable.Range(start, total - start).Where(i => double.IsFinite(xs[i]) && double.IsFinite(ys[i])).ToArray();
        double xmin = valid.Length > 0 ? xs[valid[0]] : 0, xmax = valid.Length > 0 ? xs[valid[^1]] : 1;
        double ymin = valid.Length > 0 ? valid.Min(i => ys[i]) : -1, ymax = valid.Length > 0 ? valid.Max(i => ys[i]) : 1;
        if (Bars) ymin = Math.Min(0, ymin);
        if (xmax <= xmin) xmax = xmin + 1;
        if (ymax == ymin) { double p = Math.Max(Math.Abs(ymax) * 0.01, 1e-9); ymin -= p; ymax += p; }
        double margin = (ymax - ymin) * 0.08; ymin -= margin; ymax += margin;
        Point Map(double x, double y) => new(left + (x - xmin) / (xmax - xmin) * width, top + (ymax - y) / (ymax - ymin) * height);
        for (int i = 0; i <= 4; i++)
        {
            double py = top + i * height / 4, px = left + i * width / 4;
            dc.DrawLine(grid, new(left, py), new(left + width, py));
            dc.DrawLine(grid, new(px, top), new(px, top + height));
            Label((ymax - i * (ymax - ymin) / 4).ToString("G5", CultureInfo.InvariantCulture), 2, py - 6);
            Label((xmin + i * (xmax - xmin) / 4).ToString("G5", CultureInfo.InvariantCulture), px - 8, top + height + 7);
        }
        Label(YUnit, 3, 0); Label(XUnit, left + width - 10, top + height + 21);
        if (valid.Length == 0) { Label("Sin muestras válidas", left + 18, top + height / 2); return; }
        dc.PushClip(new RectangleGeometry(new(left, top, width, height)));
        var pen = new Pen(accent, 1.25);
        if (Bars) {
            double bw = Math.Max(1, width / Math.Max(1, valid.Length) - 1);
            foreach (int i in valid) {
                var p = Map(xs[i], ys[i]); var z = Map(xs[i], 0);
                dc.DrawRectangle(accent, null, new(p.X - bw / 2, Math.Min(p.Y, z.Y), bw, Math.Max(1, Math.Abs(z.Y - p.Y))));
            }
        } else {
            // Min/max envelope preserves spikes when more than one sample maps to a pixel.
            int bucket = Math.Max(1, (total - start) / Math.Max(1, (int)width));
            Point? last = null;
            for (int i = start; i < total; i += bucket) {
                int end = Math.Min(total, i + bucket);
                if (!double.IsFinite(ys[i])) { last = null; continue; }
                double lo = ys[i], hi = lo;
                for (int j = i + 1; j < end; j++) if (double.IsFinite(ys[j])) {lo = Math.Min(lo, ys[j]); hi = Math.Max(hi, ys[j]);}
                Point p = Map(xs[i], ys[i]);
                if (last != null) dc.DrawLine(pen, last.Value, p);
                if (bucket > 1) dc.DrawLine(pen, Map(xs[i], lo), Map(xs[i], hi));
                last = Map(xs[end - 1], double.IsFinite(ys[end - 1]) ? ys[end - 1] : ys[i]);
            }
        }
        if (cursor is Point c && c.X >= left && c.X <= left + width && c.Y >= top && c.Y <= top + height) {
            dc.DrawLine(new Pen(text, 0.6), new(c.X, top), new(c.X, top + height));
            dc.DrawLine(new Pen(text, 0.6), new(left, c.Y), new(left + width, c.Y));
        }
        dc.Pop();
        if (cursor is Point pos) Label($"X {xmin + (pos.X-left)/width*(xmax-xmin):G6} {XUnit}   Y {ymax-(pos.Y-top)/height*(ymax-ymin):G6} {YUnit}", left + 8, 1, accent);
    }
}
