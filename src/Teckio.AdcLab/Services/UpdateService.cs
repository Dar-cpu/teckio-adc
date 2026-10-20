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
        return new(v, tag, root.GetProperty("body").GetString() ?? "", Asset("Teckio.AdcLab.Setup.exe"), Asset("Teckio.AdcLab.Setup.exe.sha256"));
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
        string folder = Path.Combine(Settings.Folder, "Updates", release.Version.ToString()); Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "Teckio.AdcLab.Setup.exe");
        using var response = await Http.GetAsync(release.ExeUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using (var dest = File.Create(path)) await response.Content.CopyToAsync(dest);
        string expected = (await Http.GetStringAsync(release.ShaUrl)).Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0];
        await using var file = File.OpenRead(path);
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(file));
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 no coincide. No se ejecutará la descarga.");
        return path;
    }
    public static void OpenInstaller(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("No se encontró el instalador descargado.", path);
        // Inno Setup owns replacement, shortcuts and uninstall registration.
        // Existing settings/captures live outside the installation directory.
        _ = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })
            ?? throw new IOException("Windows no pudo abrir el instalador.");
    }
}
