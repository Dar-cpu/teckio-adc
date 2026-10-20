using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
namespace Teckio.AdcLab.Services;
public sealed record ReleaseInfo(Version Version, string Tag, string Notes, string ExeUrl, string ShaUrl);
public static class UpdateService
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    static UpdateService() { Http.DefaultRequestHeaders.UserAgent.ParseAdd("TECKIO-ADC-Lab/1.0"); }
    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new(1, 0, 0);
    public static async Task<ReleaseInfo?> Check()
    {
        using var response = await Http.GetAsync("https://api.github.com/repos/Dar-cpu/teckio-adc/releases/latest");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement; string tag = root.GetProperty("tag_name").GetString()!;
        if (!Version.TryParse(tag.TrimStart('v'), out var v) || v <= new Version(Current.Major, Current.Minor, Current.Build)) return null;
        string Asset(string name) => root.GetProperty("assets").EnumerateArray().First(x => x.GetProperty("name").GetString() == name).GetProperty("browser_download_url").GetString()!;
        return new(v, tag, root.GetProperty("body").GetString() ?? "", Asset("Teckio.AdcLab.exe"), Asset("Teckio.AdcLab.exe.sha256"));
    }
    static void ValidateUrl(string url)
    {
        var u = new Uri(url);
        if (u.Scheme != "https" || u.Host != "github.com" || !u.AbsolutePath.StartsWith("/Dar-cpu/teckio-adc/releases/download/", StringComparison.Ordinal))
            throw new InvalidDataException("Origen de actualización no válido.");
    }
    public static async Task<string> Download(ReleaseInfo release)
    {
        ValidateUrl(release.ExeUrl); ValidateUrl(release.ShaUrl);
        string folder = Path.Combine(Settings.Folder, "Updates", release.Tag); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Teckio.AdcLab.exe");
        using var response = await Http.GetAsync(release.ExeUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using (var dest = File.Create(path)) await response.Content.CopyToAsync(dest);
        string expected = (await Http.GetStringAsync(release.ShaUrl)).Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        await using var file = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 no coincide. No se ejecutará la descarga.");
        return path;
    }
    public static void InstallAndRestart(string path)
    {
        string current = Environment.ProcessPath ?? throw new IOException("No se encontró el ejecutable actual.");
        if (!Path.GetFileName(current).Equals("Teckio.AdcLab.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Las actualizaciones se instalan desde la versión portable publicada, no desde dotnet run.");
        string directory = Path.GetDirectoryName(current)!;
        string probe = Path.Combine(directory, ".teckio-update-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {}
        static string Q(string s) => "'" + s.Replace("'", "''") + "'";
        string script = "$ErrorActionPreference='Stop'; " +
            $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; " +
            $"$src={Q(path)}; $dst={Q(current)}; $backup=$dst+'.previous'; " +
            "try { Copy-Item -LiteralPath $dst -Destination $backup -Force; " +
            "$done=$false; for($i=0;$i -lt 20;$i++){ try{Copy-Item -LiteralPath $src -Destination $dst -Force;$done=$true;break}catch{Start-Sleep -Milliseconds 500} }; " +
            "if(!$done){throw 'No se pudo reemplazar la aplicación'}; Start-Process -FilePath $dst " +
            "} catch { if(Test-Path -LiteralPath $backup){Copy-Item -LiteralPath $backup -Destination $dst -Force}; " +
            "Add-Type -AssemblyName PresentationFramework;[System.Windows.MessageBox]::Show($_.Exception.Message,'TECKIO - actualización');Start-Process -FilePath $dst }";
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        Process.Start(new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -EncodedCommand " + encoded) { UseShellExecute = false, CreateNoWindow = true });
    }
}
