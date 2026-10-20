using System.Globalization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Teckio.Adc.Core;
namespace Teckio.AdcLab.Services;

public static class ReportService
{
    public static void Create(string path, Capture capture, Settings settings, string[] diagnostics, uint[] registers, long crcErrors, bool rawAppendix)
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title = "TECKIO | AD7195 - Informe de caracterización"; pdf.Info.Author = settings.Operator;
        pdf.Info.Subject = capture.Info.Demo ? "DATOS SIMULADOS / DEMO" : "Adquisición de ADC, integridad y análisis DC";
        XGraphics? g = null; double y = 0; int page = 0;
        var normal = new XFont("Segoe UI", 9);
        var small = new XFont("Consolas", 7.2);
        var heading = new XFont("Segoe UI", 16, XFontStyleEx.Bold);
        var title = new XFont("Segoe UI", 26, XFontStyleEx.Bold);
        var accent = new XSolidBrush(XColor.FromArgb(8, 124, 108));
        void NewPage()
        {
            g?.Dispose(); var p = pdf.AddPage(); p.Size = PdfSharp.PageSize.A4; g = XGraphics.FromPdfPage(p); page++; y = 72;
            g.DrawRectangle(new XSolidBrush(XColor.FromArgb(18, 27, 40)), 0, 0, p.Width.Point, 45);
            g.DrawString("TECKIO  /  ADC LAB", new XFont("Segoe UI", 13, XFontStyleEx.Bold), XBrushes.White, 36, 29);
            g.DrawString(capture.Info.Demo ? "DEMO · DATOS SIMULADOS" : "AD7195 / CARACTERIZACIÓN", normal, XBrushes.White, 330, 28);
            g.DrawString($"TECKIO ADC Lab · {capture.Info.Started:yyyy-MM-dd HH:mm:ss zzz} · Página {page}", normal, XBrushes.Gray, 36, 817);
        }
        void Space(double height) { if (g == null || y + height > 790) NewPage(); }
        void Text(string value, bool bold = false)
        {
            // Wrap paragraphs without clipping long notes or file paths.
            var font = bold ? new XFont("Segoe UI", 9, XFontStyleEx.Bold) : normal;
            foreach (string paragraph in value.Replace("\r","").Split('\n')) {
                string line = "";
                foreach (string word in paragraph.Split(' ')) {
                    string next = line.Length == 0 ? word : line + " " + word;
                    if (g == null) NewPage();
                    if (g!.MeasureString(next, font).Width > 515 && line.Length > 0) {
                        Space(15); g!.DrawString(line, font, XBrushes.Black, 40, y); y += 15; line = word;
                    } else line = next;
                    while (g.MeasureString(line, font).Width > 515) {
                        int cut = Math.Min(95,line.Length);
                        while (cut>1 && g.MeasureString(line[..cut],font).Width>515)cut--;
                        Space(15);g.DrawString(line[..cut],font,XBrushes.Black,40,y);y+=15;line=line[cut..];
                    }
                }
                Space(15);g!.DrawString(line,font,XBrushes.Black,40,y);y+=15;
            }
        }
        void Section(string name) { Space(45); y += 12; g!.DrawString(name, heading, accent, 40, y); y += 25; }
        void Row(string name, string value) {
            Space(22);g!.DrawRectangle(new XSolidBrush(XColor.FromArgb(241,244,247)),40,y-11,515,20);
            g.DrawString(name,normal,XBrushes.Black,47,y+2);g.DrawString(value,normal,XBrushes.Black,290,y+2);y+=23;
        }
        void Chart(string name, double[] x, double[] values, string unit, bool bars = false)
        {
            if(values.Length<2)return;
            Space(195);Text(name,true);double top=y+5, left=88, w=450,h=130;
            double lo=values.Min(),hi=values.Max(),xlo=x[0],xhi=x[^1];
            if(bars)lo=Math.Min(lo,0);
            if(hi==lo){lo-=1e-9;hi+=1e-9;}if(xhi==xlo)xhi=xlo+1;
            for(int j=0;j<=4;j++){
                double py=top+j*h/4;
                g!.DrawLine(new XPen(XColors.LightGray,0.4),left,py,left+w,py);
                g.DrawString((hi-j*(hi-lo)/4).ToString("G4",CultureInfo.InvariantCulture),small,XBrushes.Gray,40,py+3);
            }
            int stride=Math.Max(1,values.Length/1000);XPoint? last=null;
            for(int i=0;i<values.Length;i+=stride) {
                double v=values[i],min=v,max=v;
                for(int j=i+1;j<Math.Min(values.Length,i+stride);j++){min=Math.Min(min,values[j]);max=Math.Max(max,values[j]);}
                double px=left+(x[i]-xlo)/(xhi-xlo)*w,py=top+(hi-v)/(hi-lo)*h;
                if(bars)g!.DrawLine(new XPen(XColor.FromArgb(8,124,108),Math.Max(1,w/values.Length-1)),px,top+(hi)/(hi-lo)*h,px,py);
                else {
                    if(last.HasValue)g!.DrawLine(new XPen(XColor.FromArgb(8,124,108),0.7),last.Value,new XPoint(px,py));
                    if(stride>1)g!.DrawLine(new XPen(XColor.FromArgb(8,124,108),0.7),px,top+(hi-max)/(hi-lo)*h,px,top+(hi-min)/(hi-lo)*h);
                    last=new(px,py);
                }
            }
            g!.DrawString($"{xlo:G6} → {xhi:G6}   /   {unit}",small,XBrushes.Gray,left,top+h+16);
            y=top+h+33;
        }
        static string F(double value,string unit="")=>double.IsFinite(value)?value.ToString("G8",CultureInfo.InvariantCulture)+" "+unit:"No evaluable";
        NewPage();g!.DrawString("Informe de caracterización",title,XBrushes.Black,40,y);y+=37;
        Text(capture.Info.Demo?"DEMO: todos los resultados de este informe proceden de una señal simulada.":"Captura experimental. Resultados calculados a partir de las muestras recibidas.",true);
        Section("01 / Condiciones del ensayo");
        Text("Dispositivo: "+capture.Info.Device);Text("Operador: "+settings.Operator);Text("Placa / montaje: "+settings.Board);
        Text("Inicio: "+capture.Info.Started.ToString("O"));Text("Notas: "+settings.Notes);
        Row("Referencia introducida (no medida)",F(capture.Info.Reference,"V"));
        Row("MODE / CONFIG de la captura",$"0x{capture.Info.Mode:X6} / 0x{capture.Info.Configuration:X6}");
        Row("Ganancia / polaridad",$"{Ad7195Profile.Gains[capture.Info.Configuration&7]} / {((capture.Info.Configuration&8)!=0?"Unipolar":"Bipolar")}");
        Row("Filtro / CHOP / ACX",$"{((capture.Info.Mode&0x8000)!=0?"sinc3":"sinc4")} / {(capture.Info.Configuration>>23)&1} / {(capture.Info.Configuration>>22)&1}");
        Row("FS / reloj ADC",$"{capture.Info.Mode&1023} / {(capture.Info.Mode>>18)&3}");
        Section("02 / Integridad y alcance");
        Row("Muestras recibidas / retenidas",$"{capture.Received:N0} / {capture.Samples.Length:N0}");
        Row("Fuera de ventana RAM",$"{capture.Evicted:N0}");
        Row("Saltos de índice / buffer firmware",$"{capture.IndexGaps:N0} / {capture.FirmwareDrops:N0}");
        Row("Muestras inválidas / tramas CRC",$"{capture.Invalid:N0} / {crcErrors:N0}");
        Text("Los saltos de índice y drops de firmware pueden describir la misma pérdida: no se suman. El AD7195 no dispone de contador de conversiones; sobrescrituras previas a la lectura no se pueden contar con certeza.");
        Text("El análisis se limita a la ventana retenida. La grabación CSV, si se activó al inicio, conserva las muestras recibidas fuera de esa ventana.");
        Text("CSV de sesión: "+(capture.RecordingPath??"No activado"));
        var profile=new Ad7195Profile();
        foreach(var group in capture.Samples.GroupBy(s=>s.Raw.Channel).OrderBy(s=>s.Key)){
            NewPage();int channel=group.Key;var valid=group.Where(s=>s.Valid).ToArray();
            Section($"03 / Canal {channel} · {profile.Channels[channel]}");
            Text($"Muestras válidas: {valid.Length:N0}; excluidas por flags/paridad: {group.Count()-valid.Length:N0}.");
            if(valid.Length==0){Text("Sin datos válidos para análisis.");continue;}
            double[] values=valid.Select(s=>s.Value).ToArray(),time=valid.Select(s=>s.Seconds).ToArray();
            double span=channel==2?double.NaN:Ad7195Profile.Span(capture.Info.Configuration,capture.Info.Reference);
            var s=Analysis.Compute(values,time,span);string unit=channel==2?"°C":"V";
            Row("Media / RMS total",F(s.Mean,unit)+" / "+F(s.Rms,unit));
            Row("Mínimo / máximo",F(s.Min,unit)+" / "+F(s.Max,unit));
            Row("Desviación estándar / pico a pico",F(s.Sigma,unit)+" / "+F(s.PeakToPeak,unit));
            Row("Resolución efectiva DC",F(s.EffectiveResolution,"bits"));
            Row("Resolución libre de ruido",F(s.NoiseFreeBits,"bits"));
            Row("Tasa medida del canal / jitter",F(s.Rate,"S/s")+" / "+F(s.IntervalJitterUs,"µs"));
            if(channel!=2)Row("Error de media respecto al valor declarado",F(s.Mean-settings.Target,"V"));
            Chart("Forma de onda (muestras válidas)",time,values,"s / "+unit);
            var histogram=Analysis.Histogram(values);Chart("Histograma",histogram.X,histogram.Y,unit+" / ocurrencias",true);
            bool spectral=channel!=2&&capture.IndexGaps==0&&capture.Invalid==0&&s.IntervalJitterUs*s.Rate<20000;
            var fft=spectral?Analysis.Fft(values,s.Rate,span):new Spectrum([],[],0,0,"Espectro omitido por integridad/temporización o canal de temperatura.");
            if(fft.Length>=32){Chart($"Espectro Hann · N={fft.Length} · pico {fft.PeakHz:F3} Hz",fft.Frequency,fft.Dbfs,"Hz / dBFS");Text(fft.Note);}
            else Text(fft.Note);
            if(channel==2)Text("Temperatura interna aproximada: (código − 2^23)/2815 − 273.15, prevista para configuración bipolar y ganancia 1.");
        }
        NewPage();Section("04 / Diagnóstico y registros");
        Text("Última lectura manual de registros (puede ser anterior o posterior a la captura; no sustituye a MODE/CONFIG guardados en la sesión).");
        for(int i=0;i<8;i++)Row(Ad7195Profile.Registers[i],$"0x{registers[i]:X6}");
        foreach(string d in diagnostics)Text(d);
        if(diagnostics.Length==0)Text("No se ejecutaron pruebas de diagnóstico en esta sesión.");
        Section("05 / Metodología e interpretación");
        Text("σ: desviación estándar muestral (N−1). RMS total: raíz de la media del cuadrado; incluye componente DC. Ruido RMS: σ, sólo interpretable como ruido con entrada estable.");
        Text("Resolución efectiva DC = log2(rango/σ). Libre de ruido = log2(rango/pico a pico). Sin dispersión medida se indica no evaluable, no una resolución infinita.");
        Text("Rango bipolar = 2·VREF/G; unipolar = VREF/G. Conversión de códigos según AD7195. VREF y valor esperado son introducidos por el operador.");
        Text("Tiempo: contador PIC de 3 MHz extendido en la PC. Marca de lectura observada por polling, no timestamp interno de la conversión; incluye latencia de servicio.");
        Text("No se han medido INL, DNL, exactitud absoluta, rechazo de alimentación, deriva térmica ni ENOB/SINAD dinámico. Requieren excitación y patrones apropiados.");
        Text("Referencias: Analog Devices, AD7195 data sheet; Microchip, PIC18(L)F2X/45K50 DS30000684B. Firmware USB: HFINTOSC 16 MHz ×3 con ACT.");
        if(rawAppendix){
            NewPage();Section("06 / Anexo crudo (hasta 200 muestras)");
            Text("Datos completos de la ventana: exportación CSV/JSON. Últimas filas de la captura:");
            foreach(var m in capture.Samples.TakeLast(200)){
                Space(12);g!.DrawString($"{m.Raw.Index,8} {m.Seconds,11:F6}s CH{m.Raw.Channel} 0x{m.Raw.Code:X6} ST {m.Raw.Status:X2} {m.Value,15:G9} {(m.Valid?"OK":"INVALID")}",small,XBrushes.Black,40,y);y+=12;
            }
        }
        g?.Dispose();pdf.Save(path);
    }
}
