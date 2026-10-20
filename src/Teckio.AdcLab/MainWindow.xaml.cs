using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Teckio.Adc.Core;
using Teckio.AdcLab.Services;
namespace Teckio.AdcLab;

public partial class MainWindow : Window
{
    readonly DeviceClient device = new();
    readonly Ad7195Profile profile = new();
    readonly Settings settings = Settings.Load();
    readonly DispatcherTimer timer = new();
    readonly CheckBox[] channels = new CheckBox[8], stimEnable = new CheckBox[7], stimHigh = new CheckBox[7];
    Session? session;
    bool running, demo, busy, initialized;
    uint mode = 0x182060, config = 0x140, demoIndex;
    readonly Random random = new(7195);
    readonly List<string> diagnostics = [];
    uint[] registers = new uint[8];
    int tickNumber;
    long captureCrcStart;
    public MainWindow()
    {
        InitializeComponent();
        for (int i = 0; i < 8; i++) {
            channels[i] = new CheckBox { Content = $"{i} · {profile.Channels[i]}" };
            ChannelChecks.Children.Add(channels[i]);
        }
        for (int i = 0; i < 7; i++) {
            stimEnable[i] = new CheckBox { Content = new[] {"AIN1 / RA0","AIN2 / RA1","AIN3 / RA2","AIN4 / RA3","AINCOM / RB1","REF+ / RB2","REF− / RB3"}[i] };
            stimHigh[i] = new CheckBox { Content = "Nivel alto (sin marcar: bajo)" };
            StimulusControls.Children.Add(stimEnable[i]); StimulusControls.Children.Add(stimHigh[i]);
        }
        ViewChannel.ItemsSource = profile.Channels; ViewChannel.SelectedIndex = 0;
        GainBox.ItemsSource = new[] {1,8,16,32,64,128};
        RegisterSelect.ItemsSource = Ad7195Profile.Registers; RegisterSelect.SelectedIndex = 2;
        mode = settings.Mode; config = settings.Configuration; PutConfig();
        ReferenceBox.Text = settings.Reference.ToString(CultureInfo.InvariantCulture);
        OperatorBox.Text = settings.Operator; BoardBox.Text = settings.Board; NotesBox.Text = settings.Notes;
        TargetBox.Text = settings.Target.ToString(CultureInfo.InvariantCulture);
        CapacityBox.Text = settings.MemorySamples.ToString(); RefreshBox.Text = settings.RefreshMs.ToString();
        ThemeBox.SelectedIndex = settings.Theme == "Claro" ? 1 : 0;
        ApplyTheme(); RefreshPorts();
        device.Trace += s => Dispatcher.BeginInvoke(() => Log(s));
        device.Sample += (s, d) => { if(running) session?.Add(s, d); };
        device.Fault += e => Dispatcher.BeginInvoke(() => {
            running = false; session?.Dispose(); device.Dispose(); UpdateState(); Log("ERROR: " + e.Message); StatusLine.Text = "Conexión interrumpida: " + e.Message;
        });
        timer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(settings.RefreshMs, 50, 2000));
        timer.Tick += (_, _) => Tick(); timer.Start();
        VersionLabel.Text = $"TECKIO ADC Lab {UpdateService.Current.ToString(3)} · Windows x64 · .NET 10";
        Closing += (_, _) => { timer.Stop(); session?.Dispose(); device.Dispose(); };
        initialized = true; UpdateState();
    }
    void UpdateState()
    {
        ConnectButton.Content = device.Connected ? "Desconectar" : "Conectar";
        StartButton.IsEnabled = !running && !busy; StopButton.IsEnabled = running && !busy;
        ConfigPanel.IsEnabled = !running && !busy; DemoCheck.IsEnabled = !running && !device.Connected && !busy;
        Ports.IsEnabled = !device.Connected && !running;
        DeviceLabel.Text = demo ? "DEMO · señal generada, sin conexión al ADC" : device.Identity;
        RunBadge.Text = running ? (demo ? "●  DEMO / SIMULADO" : "●  ADQUIRIENDO") : (device.Connected ? "●  CONECTADO" : "●  DETENIDO");
    }
    async Task Guard(Func<Task> action)
    {
        if (busy) return;
        busy = true; UpdateState();
        try { await action(); } catch (Exception e) { Log("ERROR: " + e.Message); StatusLine.Text = e.Message; MessageBox.Show(this, e.Message, "TECKIO ADC Lab", MessageBoxButton.OK, MessageBoxImage.Information); }
        finally { busy = false; UpdateState(); }
    }
    void RequireStopped() { if (running) throw new InvalidOperationException("Detén la captura antes de esta operación."); }
    void Log(string message)
    {
        ConsoleLog.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        if (ConsoleLog.Text.Length > 80000) ConsoleLog.Text = ConsoleLog.Text[^60000..];
        ConsoleLog.ScrollToEnd();
    }
    void Diagnostic(string s) { diagnostics.Add($"{DateTimeOffset.Now:O} {s}"); DiagnosticLog.AppendText(s + "\n"); DiagnosticLog.ScrollToEnd(); Log(s); }
    static double Number(string text) => double.Parse(text.Replace(',', '.'), CultureInfo.InvariantCulture);
    static uint Hex(string text) => uint.Parse(text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    void RefreshPorts()
    {
        Ports.ItemsSource = SerialPort.GetPortNames().Order().ToArray();
        Ports.SelectedItem = settings.LastPort;
        if (Ports.SelectedIndex < 0 && Ports.Items.Count > 0) Ports.SelectedIndex = 0;
    }
    void RefreshPortsClick(object sender, RoutedEventArgs e) => RefreshPorts();
    async void ConnectClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        if (device.Connected) { if (running) await Stop(); device.Dispose(); demo = false; }
        else {
            RequireStopped(); demo = false; DemoCheck.IsChecked = false;
            await device.Connect(Ports.SelectedItem as string ?? throw new InvalidOperationException("Selecciona un puerto COM."));
            settings.LastPort = (string)Ports.SelectedItem; await ReadRegisters(); PutConfig(); StatusLine.Text = device.Identity;
        }
    });
    void GetConfig()
    {
        uint fs = uint.Parse(FsBox.Text);
        if (fs < 1 || fs > 1023) throw new InvalidOperationException("FS debe estar entre 1 y 1023.");
        int gain = (int)(GainBox.SelectedItem ?? 1);
        config = (uint)Array.IndexOf(Ad7195Profile.Gains, gain);
        for (int i = 0; i < 8; i++) if (channels[i].IsChecked == true) config |= 1u << (8 + i);
        if (UnipolarCheck.IsChecked == true) config |= 8;
        if (BufferCheck.IsChecked == true) config |= 16;
        if (BurnoutCheck.IsChecked == true) config |= 128;
        if (RefDetectCheck.IsChecked == true) config |= 64;
        if (ChopCheck.IsChecked == true) config |= 0x800000;
        if (AcxCheck.IsChecked == true) config |= 0x400000;
        mode = fs | ((uint)ClockBox.SelectedIndex << 18) | 0x100000;
        if (Sinc3Check.IsChecked == true) mode |= 0x8000;
        if (ParityCheck.IsChecked == true) mode |= 0x2000;
        if (SingleCycleCheck.IsChecked == true) mode |= 0x800;
        if (Rej60Check.IsChecked == true) mode |= 0x400;
        string error = profile.Validate(mode, config); if (error != "") throw new InvalidOperationException(error);
        double reference = Number(ReferenceBox.Text);
        if (!double.IsFinite(reference) || reference <= 0 || reference > 5.25) throw new InvalidOperationException("VREF debe ser >0 y ≤5.25 V.");
        settings.Reference = reference; settings.Mode = mode; settings.Configuration = config;
        ConfigSummary.Text = $"MODE 0x{mode:X6}   CONFIG 0x{config:X6}\nTasa agregada nominal ≈ {Ad7195Profile.NominalRate(mode, config):F3} S/s (reloj nominal).";
    }
    void PutConfig()
    {
        FsBox.Text = (mode & 1023).ToString();
        ClockBox.SelectedIndex = (int)((mode >> 18) & 3);
        GainBox.SelectedItem = Ad7195Profile.Gains[config & 7];
        for (int i = 0; i < 8; i++) channels[i].IsChecked = (config & (1u << (i + 8))) != 0;
        UnipolarCheck.IsChecked = (config & 8) != 0; BufferCheck.IsChecked = (config & 16) != 0;
        RefDetectCheck.IsChecked = (config & 64) != 0; BurnoutCheck.IsChecked = (config & 128) != 0;
        ChopCheck.IsChecked = (config & 0x800000) != 0; AcxCheck.IsChecked = (config & 0x400000) != 0;
        Sinc3Check.IsChecked = (mode & 0x8000) != 0; ParityCheck.IsChecked = (mode & 0x2000) != 0;
        SingleCycleCheck.IsChecked = (mode & 0x800) != 0; Rej60Check.IsChecked = (mode & 0x400) != 0;
        ConfigSummary.Text = $"MODE 0x{mode:X6}   CONFIG 0x{config:X6}";
    }
    async Task ApplyConfig()
    {
        RequireStopped(); GetConfig();
        if (DemoCheck.IsChecked != true) {
            await device.Command(0x12, Wire.Words(mode, config));
            uint m = await device.ReadRegister(1), c = await device.ReadRegister(2);
            if (m != mode || c != config) throw new IOException("Falló verificación de configuración.");
        }
        settings.Save(); StatusLine.Text = "Configuración aplicada y verificada" + (DemoCheck.IsChecked == true ? " (DEMO)" : "");
    }
    async void ApplyClick(object sender, RoutedEventArgs e) => await Guard(ApplyConfig);
    async void ReadConfigClick(object sender, RoutedEventArgs e) => await Guard(async () => { RequireStopped(); await ReadRegisters(); PutConfig(); });
    void PresetClick(object sender, RoutedEventArgs e)
    {
        FsBox.Text = (string)((Button)sender).Tag; ChopCheck.IsChecked = AcxCheck.IsChecked = SingleCycleCheck.IsChecked = false;
        Sinc3Check.IsChecked = false; ClockBox.SelectedIndex = 2;
    }
    async Task Start(bool one = false)
    {
        RequireStopped(); await ApplyConfig(); demo = DemoCheck.IsChecked == true;
        session?.Dispose();
        session = new(new(DateTimeOffset.Now, demo, demo ? "DEMO / SIMULADO" : device.Identity, mode, config, settings.Reference),
            Math.Clamp(settings.MemorySamples, 4096, 2000000));
        if (RecordCheck.IsChecked == true) {
            var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = $"TECKIO_AD7195_{DateTime.Now:yyyyMMdd_HHmmss}.csv" };
            if (dialog.ShowDialog(this) != true) return;
            session.StartRecording(dialog.FileName); RecordLabel.Text = "REC · " + Path.GetFileName(dialog.FileName);
            File.WriteAllText(dialog.FileName + ".json", JsonSerializer.Serialize(session.Info, JsonOptions));
        } else RecordLabel.Text = "Ventana RAM · activa CSV para conservar toda la sesión";
        demoIndex = 0; captureCrcStart = device.CorruptFrames; running = true;
        if (!demo) await device.Command(one ? (byte)0x22 : (byte)0x20);
        else if (one) { AddDemo(1); running = false; }
        if (one && !demo) {
            var deadline = DateTime.UtcNow.AddSeconds(6);
            while (session.Snapshot(1).Received == 0 && DateTime.UtcNow < deadline) await Task.Delay(30);
            await Stop();
            if (session.Snapshot(1).Received == 0) throw new TimeoutException("No hubo conversión en 6 s. Revisa referencia, reloj y RDY.");
        }
        StatusLine.Text = demo ? "DEMO: datos simulados, no representan el ADC físico." : "Capturando paquetes binarios USB CDC.";
    }
    async Task Stop()
    {
        if (!demo && device.Connected) {
            await device.Command(0x21);
            // STOP ACK is prioritised before the remaining sample packets; drain while session stays alive.
            await Task.Delay(100);
            byte[] status = await device.Command(0x42); Log("STATS " + Convert.ToHexString(status));
            session?.FinalizeCounts(Wire.U32(status),Wire.U32(status.AsSpan(4)));
        }
        running = false; session?.Dispose(); UpdatePlots(); StatusLine.Text = "Captura detenida.";
    }
    async void StartClick(object sender, RoutedEventArgs e) => await Guard(async () => { try { await Start(); } catch { running = false; session?.Dispose(); throw; } });
    async void SingleClick(object sender, RoutedEventArgs e) => await Guard(async () => { try { await Start(true); } catch { running = false; session?.Dispose(); throw; } });
    async void StopClick(object sender, RoutedEventArgs e) => await Guard(Stop);
    void AddDemo(int n)
    {
        int[] enabled = Enumerable.Range(0, 8).Where(i => (config & (1u << (8 + i))) != 0).ToArray();
        double rate = Math.Max(1, Ad7195Profile.NominalRate(mode, config));
        for (int i = 0; i < n; i++) {
            int channel = enabled[demoIndex % enabled.Length]; double t = demoIndex / rate;
            double v = (0.03 * Math.Sin(2 * Math.PI * 1.2 * t) + (random.NextDouble() - 0.5) * 0.0003) / Ad7195Profile.Gains[config & 7];
            uint code = channel == 2 ? (uint)(8388608 + (25 + 273.15) * 2815) :
                (uint)Math.Clamp(((config & 8) != 0 ? (v + 0.02 / Ad7195Profile.Gains[config & 7]) / settings.Reference * Ad7195Profile.Gains[config & 7] * 16777216 :
                (1 + v / settings.Reference * Ad7195Profile.Gains[config & 7]) * 8388608), 0, 16777215);
            byte status = (byte)(channel | ((System.Numerics.BitOperations.PopCount(code) & 1) != 0 ? 0x10 : 0));
            session?.Add(new(demoIndex++, (uint)(t * 3000000), code, status), 0);
        }
    }
    void Tick()
    {
        if (running && demo) AddDemo(Math.Max(1, (int)(Ad7195Profile.NominalRate(mode, config) * timer.Interval.TotalSeconds)));
        if (FreezePlot.IsChecked != true) UpdatePlots();
    }
    void UpdatePlots()
    {
        if (session == null) return;
        int window = new[] {1024,4096,16384,65536}[Math.Clamp(WindowSize.SelectedIndex, 0, 3)];
        var snapshot = session.Snapshot(window);
        int channel = Math.Max(0, ViewChannel.SelectedIndex);
        var selected = snapshot.Samples.Where(s => s.Raw.Channel == channel).ToArray();
        var valid = selected.Where(s => s.Valid).ToArray();
        double[] values = valid.Select(s => s.Value).ToArray(), times = valid.Select(s => s.Seconds).ToArray();
        double span = channel == 2 ? double.NaN : Ad7195Profile.Span(snapshot.Info.Configuration, snapshot.Info.Reference);
        var stats = Analysis.Compute(values, times, span);
        string unit = channel == 2 ? "°C" : "V";
        ValueLabel.Text = selected.Length == 0 ? "—" : $"{selected[^1].Value:F7} {unit}";
        RawLabel.Text = selected.Length == 0 ? "0x------" : $"0x{selected[^1].Raw.Code:X6}  ST 0x{selected[^1].Raw.Status:X2}";
        NoiseLabel.Text = $"{stats.Sigma:G5} {unit}"; PpLabel.Text = $"P-P {stats.PeakToPeak:G5} {unit}";
        RateLabel.Text = $"{stats.Rate:F2} S/s"; CountLabel.Text = $"{snapshot.Received:N0} recibidas · {snapshot.Evicted:N0} fuera de RAM";
        long crc = demo ? 0 : device.CorruptFrames - captureCrcStart;
        bool ok = snapshot.IndexGaps == 0 && snapshot.FirmwareDrops == 0 && snapshot.Invalid == 0 && crc == 0;
        IntegrityLabel.Text = ok ? "Correcta" : "Revisar";
        LossLabel.Text = $"CRC {crc} · gaps {snapshot.IndexGaps} · ADC {snapshot.Invalid}";
        WavePlot.XUnit = "s"; WavePlot.YUnit = RawPlot.IsChecked == true ? "código" : unit;
        WavePlot.Set(selected.Select(s => s.Seconds).ToArray(), selected.Select(s => !s.Valid ? double.NaN : RawPlot.IsChecked == true ? (double)s.Raw.Code : s.Value).ToArray());
        if (++tickNumber % 5 == 0 || !running) {
            var h = Analysis.Histogram(values);
            HistogramPlot.XUnit = unit; HistogramPlot.YUnit = "N"; HistogramPlot.Set(h.X, h.Y);
            bool uniform = stats.Count > 31 && snapshot.IndexGaps == 0 && snapshot.Invalid == 0 &&
                stats.IntervalJitterUs * stats.Rate < 20000 && channel != 2;
            var fft = uniform ? Analysis.Fft(values, stats.Rate, span) : new Spectrum([], [], 0, 0, "Se requiere captura uniforme sin pérdidas.");
            FftPlot.XUnit = "Hz"; FftPlot.YUnit = "dBFS"; FftPlot.Set(fft.Frequency, fft.Dbfs);
            FftLabel.Text = fft.Length >= 32 ? $"FFT / HANN · N {fft.Length} · pico {fft.PeakHz:F2} Hz" : "FFT / no disponible: muestras o integridad";
            SamplesGrid.ItemsSource = snapshot.Samples.TakeLast(500).Reverse().Select(s => new {
                N=s.Raw.Index, Tiempo_s=s.Seconds.ToString("F6"), Canal=s.Raw.Channel, Crudo=s.Raw.Code, HEX=$"{s.Raw.Code:X6}", Estado=$"{s.Raw.Status:X2}", Valor=s.Value.ToString("G10"), Unidad=s.Raw.Channel==2?"°C":"V", Válida=s.Valid
            }).ToArray();
        }
    }
    void PlotOptionChanged(object sender, RoutedEventArgs e) { if (initialized) UpdatePlots(); }
    void AutoScaleClick(object sender, RoutedEventArgs e) { WavePlot.AutoScale(); HistogramPlot.AutoScale(); FftPlot.AutoScale(); }
    async Task ReadRegisters()
    {
        RequireStopped(); byte[] data = await device.Command(2);
        for (int i = 0; i < 8; i++) registers[i] = Wire.U32(data.AsSpan(4 * i));
        mode = registers[1]; config = registers[2];
        RegistersGrid.ItemsSource = registers.Select((v,i)=>new { Dirección=$"0x{i:X2}", Registro=Ad7195Profile.Registers[i], HEX=$"0x{v:X6}", Decimal=v, Bits=Convert.ToString(v,2).PadLeft(i is 0 or 4 or 5?8:24,'0'), Acceso=i is 0 or 3 or 4?"R":"R/W" }).ToArray();
    }
    async void ReadRegistersClick(object sender, RoutedEventArgs e) => await Guard(ReadRegisters);
    async void WriteRegisterClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        RequireStopped(); await device.WriteRegister((byte)RegisterSelect.SelectedIndex, Hex(RegisterValue.Text)); await ReadRegisters(); PutConfig();
    });
    async void DiagnosticClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        RequireStopped(); await ReadRegisters();
        Diagnostic($"ID 0x{registers[4]:X2}: {(registers[4] == 0xA6 ? "PASS" : "FAIL")}");
        uint original = registers[5]; uint a=0,b=0,c=0;
        try {
            await device.WriteRegister(5, 0, false); a = await device.ReadRegister(5);
            await device.WriteRegister(5, 0x40, false); b = await device.ReadRegister(5);
            await device.WriteRegister(5, 0, false); c = await device.ReadRegister(5);
        } finally { await device.WriteRegister(5, original & 0x40, false); }
        Diagnostic($"BPDSW: {a:X2} → {b:X2} → {c:X2}; extra=0x{(a|b|c)&0xBF:X2}; {(a==0&&b==0x40&&c==0?"PASS":"FAIL")}");
        Diagnostic("Las pruebas de registros no certifican la precisión analógica.");
    });
    async Task Pins()
    {
        byte[] p = await device.Command(0x41);
        string[] names = ["RDY","BPDSW","ACX1","/ACX1","ACX2","/ACX2","RB0","MCLK2"];
        PinsLabel.Text = string.Join("   ", names.Select((s,i)=>$"{s}={(p[0]>>i)&1}"));
        Diagnostic(PinsLabel.Text);
    }
    async void PinsClick(object sender, RoutedEventArgs e) => await Guard(Pins);
    async void ClockTestClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        RequireStopped(); uint original = await device.ReadRegister(1);
        try {
            await device.WriteRegister(1, (original & ~0xEC0000u) | 0x0C0000);
            await Task.Delay(20); byte[] a = await device.Command(0x41); await Task.Delay(500); byte[] b = await device.Command(0x41);
            ushort ca = (ushort)(a[3] | a[4] << 8), cb = (ushort)(b[3] | b[4] << 8);
            double dt = unchecked(Wire.U32(b.AsSpan(5)) - Wire.U32(a.AsSpan(5))) / 3000000.0;
            double hz = unchecked((ushort)(cb-ca)) * 256 / dt;
            Diagnostic($"MCLK2 por RA4/T0CKI: {hz:N0} Hz · ventana {dt:F4}s (prescaler 256).");
        } finally { await device.WriteRegister(1, original); }
    });
    async void SyncClick(object sender, RoutedEventArgs e) => await Guard(async () => {RequireStopped(); await device.Command(0x23);});
    async void ResetClick(object sender, RoutedEventArgs e) => await Guard(async () => {RequireStopped(); await device.Command(0x24); await ReadRegisters(); PutConfig();});
    async Task Calibrate(byte kind)
    {
        RequireStopped();
        if (kind >= 6 && MessageBox.Show(this, "¿Está aplicado el nivel externo correspondiente a esta calibración de sistema?", "Calibración", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        await device.Command(0x30, [kind]); var deadline = DateTime.UtcNow.AddSeconds(20);
        while ((await device.ReadRegister(0) & 0x80) != 0) { if(DateTime.UtcNow>deadline) throw new TimeoutException("Calibración sin RDY en 20 s."); await Task.Delay(50); }
        await ReadRegisters(); PutConfig(); Diagnostic($"Calibración {kind} finalizada · OFFSET={registers[6]:X6} · FULLSCALE={registers[7]:X6}");
    }
    async void CalibrationClick(object sender, RoutedEventArgs e) => await Guard(()=>Calibrate(byte.Parse((string)((Button)sender).Tag)));
    async void StimulusClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        RequireStopped(); byte d=0,v=0; for(int i=0;i<7;i++) {if(stimEnable[i].IsChecked==true)d|=(byte)(1<<i);if(stimHigh[i].IsChecked==true)v|=(byte)(1<<i);}
        await device.Command(0x40,[d,v]); Diagnostic($"Estímulos: dirección 0x{d:X2}, niveles 0x{v:X2}");
    });
    async void ReleasePinsClick(object sender, RoutedEventArgs e) => await Guard(async () => {RequireStopped(); await device.Command(0x40,[0,0]);foreach(var c in stimEnable)c.IsChecked=false;});
    async void FixtureClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        RequireStopped(); await device.Command(0x40,[0x7F,0x20]);
        for(int i=0;i<7;i++){stimEnable[i].IsChecked=true;stimHigh[i].IsChecked=i==5;}
        Diagnostic("Fixture original: AIN1..4/AINCOM=0, REF+=alto, REF−=0. Referencia por GPIO, no patrón metrológico.");
    });
    async Task ConsoleCommand()
    {
        string[] p = ConsoleInput.Text.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(p.Length==0)return;
        switch(p[0].ToUpperInvariant()) {
            case "HELP": Log("INFO REGS READ <reg> WRITE <reg> <hex> SYNC RESET PINS STATS CAL <4..7> GPIO <dir hex> <value hex>");break;
            case "INFO": await device.Command(1);break;
            case "REGS": await ReadRegisters();break;
            case "READ" when p.Length==2: RequireStopped(); Log($"0x{await device.ReadRegister(byte.Parse(p[1])):X6}");break;
            case "WRITE" when p.Length==3: RequireStopped(); await device.WriteRegister(byte.Parse(p[1]),Hex(p[2]));await ReadRegisters();PutConfig();break;
            case "SYNC": RequireStopped();await device.Command(0x23);break;
            case "RESET": RequireStopped();await device.Command(0x24);await ReadRegisters();PutConfig();break;
            case "PINS": await Pins();break;
            case "STATS": await device.Command(0x42);break;
            case "CAL" when p.Length==2: await Calibrate(byte.Parse(p[1]));break;
            case "GPIO" when p.Length==3: RequireStopped();await device.Command(0x40,[(byte)Hex(p[1]),(byte)Hex(p[2])]);break;
            default: throw new InvalidOperationException("Comando o argumentos incorrectos. Escribe HELP.");
        }
    }
    async void ConsoleClick(object sender, RoutedEventArgs e) => await Guard(ConsoleCommand);
    async void ConsoleKeyDown(object sender, KeyEventArgs e) {if(e.Key==Key.Enter)await Guard(ConsoleCommand);}
    static readonly JsonSerializerOptions JsonOptions = new() {WriteIndented=true, NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals};
    Capture CaptureNow() => session?.Snapshot() ?? throw new InvalidOperationException("Realiza una captura primero.");
    async void ExportCsvClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        var capture=CaptureNow();var d=new SaveFileDialog{Filter="CSV|*.csv",FileName="TECKIO_AD7195.csv"};
        if(d.ShowDialog(this)!=true)return;
        await Task.Run(()=>{using var w=new StreamWriter(d.FileName,false);w.WriteLine(Session.Header);foreach(var s in capture.Samples)w.WriteLine(Session.Csv(s));});
        File.WriteAllText(d.FileName+".json",JsonSerializer.Serialize(capture.Info,JsonOptions));StatusLine.Text=$"Exportadas {capture.Samples.Length:N0} muestras.";
    });
    async void ExportSessionClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        var c=CaptureNow();var d=new SaveFileDialog{Filter="Sesión JSON|*.json",FileName="TECKIO_AD7195_session.json"};
        if(d.ShowDialog(this)==true)await File.WriteAllTextAsync(d.FileName,JsonSerializer.Serialize(c,JsonOptions));
    });
    void ReadReportFields() {
        settings.Operator=OperatorBox.Text;settings.Board=BoardBox.Text;settings.Notes=NotesBox.Text;
        settings.Target=Number(TargetBox.Text);if(!double.IsFinite(settings.Target))throw new InvalidOperationException("Valor esperado no válido.");
    }
    async void ReportClick(object sender, RoutedEventArgs e) => await Guard(async () => {
        var c=CaptureNow();ReadReportFields();
        var d=new SaveFileDialog{Filter="Informe PDF|*.pdf",FileName=$"TECKIO_AD7195_{DateTime.Now:yyyyMMdd_HHmm}.pdf"};
        if(d.ShowDialog(this)!=true)return;
        string[] logs=diagnostics.ToArray();bool raw=AppendRawCheck.IsChecked==true;
        await Task.Run(()=>ReportService.Create(d.FileName,c,settings,logs,registers,demo?0:device.CorruptFrames-captureCrcStart,raw));
        settings.Save();StatusLine.Text="Reporte creado: "+d.FileName;
    });
    async void SaveProfileClick(object sender,RoutedEventArgs e)=>await Guard(async()=>{
        GetConfig();var d=new SaveFileDialog{Filter="Perfil JSON|*.json",FileName="AD7195_profile.json"};
        if(d.ShowDialog(this)==true)await File.WriteAllTextAsync(d.FileName,JsonSerializer.Serialize(settings,JsonOptions));
    });
    async void LoadProfileClick(object sender,RoutedEventArgs e)=>await Guard(async()=>{
        RequireStopped();var d=new OpenFileDialog{Filter="Perfil JSON|*.json"};if(d.ShowDialog(this)!=true)return;
        var s=JsonSerializer.Deserialize<Settings>(await File.ReadAllTextAsync(d.FileName))??throw new IOException("Perfil vacío.");
        string error=profile.Validate(s.Mode,s.Configuration);if(error!="")throw new IOException(error);
        mode=s.Mode;config=s.Configuration;ReferenceBox.Text=s.Reference.ToString(CultureInfo.InvariantCulture);PutConfig();GetConfig();
        StatusLine.Text="Perfil cargado. Pulsa Aplicar para enviarlo al dispositivo.";
    });
    async void SaveSettingsClick(object sender,RoutedEventArgs e)=>await Guard(()=>{
        RequireStopped();settings.MemorySamples=Math.Clamp(int.Parse(CapacityBox.Text),4096,2000000);
        settings.RefreshMs=Math.Clamp(int.Parse(RefreshBox.Text),50,2000);timer.Interval=TimeSpan.FromMilliseconds(settings.RefreshMs);
        ReadReportFields();settings.Save();StatusLine.Text="Preferencias guardadas.";return Task.CompletedTask;
    });
    void ThemeChanged(object sender,SelectionChangedEventArgs e){if(initialized){ApplyTheme();settings.Save();}}
    void ApplyTheme() {
        bool light=ThemeBox.SelectedIndex==1;settings.Theme=light?"Claro":"Oscuro";
        string[] keys=["Bg","Panel","Input","Line","Text","Muted","Accent"];
        string[] colors=light?["#F0F3F7","#FFFFFF","#E7EDF4","#CDD6E2","#172333","#5A6C80","#087C6C"]:["#0D1016","#151B24","#1E2734","#2B3646","#EEF3FA","#9BAABD","#49D9C0"];
        for(int i=0;i<keys.Length;i++) Application.Current.Resources[keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        Brand.Source=new BitmapImage(new Uri("pack://application:,,,/Assets/logo_solo_fondo_"+(light?"blanco":"negro")+".png"));
        WavePlot.InvalidateVisual();HistogramPlot.InvalidateVisual();FftPlot.InvalidateVisual();
    }
    async void UpdateClick(object sender,RoutedEventArgs e)=>await Guard(async()=>{
        RequireStopped();StatusLine.Text="Consultando GitHub Releases…";var release=await UpdateService.Check();
        if(release==null){StatusLine.Text="No hay una versión estable más reciente publicada.";return;}
        if(MessageBox.Show(this,$"Versión {release.Tag} disponible.\n\n¿Descargar y verificar el instalador? La aplicación se cerrará y se abrirá el asistente de actualización. Tus preferencias se conservarán.","Actualizar ADC Lab",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;
        string path=await UpdateService.Download(release);UpdateService.OpenInstaller(path);Application.Current.Shutdown();
    });
    public async Task SmokeTest(string folder)
    {
        Directory.CreateDirectory(folder);DemoCheck.IsChecked=true;await Start();
        AddDemo(4096);running=false;UpdatePlots();UpdateLayout();await Task.Delay(300);
        var bitmap=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(folder,"dark.png")))encoder.Save(f);
        ThemeBox.SelectedIndex=1;UpdateLayout();await Task.Delay(200);
        bitmap=new((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(this);
        encoder=new();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(Path.Combine(folder,"light.png")))encoder.Save(f);
        ReportService.Create(Path.Combine(folder,"demo-report.pdf"),CaptureNow(),settings,["DEMO / smoke test"],registers,0,true);
    }
}
