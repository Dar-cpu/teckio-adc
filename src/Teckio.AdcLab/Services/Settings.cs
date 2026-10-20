using System.IO;
using System.Text.Json;
namespace Teckio.AdcLab.Services;
public sealed class Settings
{
    public string Theme { get; set; } = "Oscuro";
    public double Reference { get; set; } = 5;
    public uint Mode { get; set; } = 0x182060;
    public uint Configuration { get; set; } = 0x000140;
    public int MemorySamples { get; set; } = 262144;
    public int RefreshMs { get; set; } = 100;
    public string LastPort { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Board { get; set; } = "";
    public double Target { get; set; }
    public string Notes { get; set; } = "";
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TECKIO", "ADC Lab");
    public static Settings Load() {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new(); }
        catch (Exception e) when (e is IOException or JsonException) { return new(); }
    }
    public void Save() {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "settings.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
