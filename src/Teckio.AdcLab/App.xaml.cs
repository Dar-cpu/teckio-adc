using System.IO;
using System.Windows;
namespace Teckio.AdcLab;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try {
            var window = new MainWindow(); MainWindow = window; window.Show();
            if (e.Args.Length == 2 && e.Args[0] == "--smoke-test") {
                await window.SmokeTest(e.Args[1]); Shutdown(0);
            }
        } catch (Exception ex) {
            if (e.Args.Length == 2 && e.Args[0] == "--smoke-test") {
                Directory.CreateDirectory(e.Args[1]);File.WriteAllText(Path.Combine(e.Args[1],"error.txt"),ex.ToString());Shutdown(1);
            } else { MessageBox.Show(ex.ToString(),"TECKIO ADC Lab");Shutdown(1); }
        }
    }
}
