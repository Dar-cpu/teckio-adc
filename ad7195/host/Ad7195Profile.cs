namespace Teckio.Adc.Core;
public sealed class Ad7195Profile : IAdcProfile
{
    public string Name => "AD7195";
    public string[] Channels => ["AIN1 − AIN2", "AIN3 − AIN4", "Temperatura interna", "AIN2 − AIN2",
        "AIN1 − AINCOM", "AIN2 − AINCOM", "AIN3 − AINCOM", "AIN4 − AINCOM"];
    public static readonly int[] Gains = [1, 0, 0, 8, 16, 32, 64, 128];
    public static readonly string[] Registers = ["STATUS", "MODE", "CONFIG", "DATA", "ID", "GPOCON", "OFFSET", "FULLSCALE"];
    public double Convert(uint code, uint config, double reference)
    {
        int gain = Gains[config & 7];
        if (gain == 0) return double.NaN;
        return (config & 8) != 0 ? code / 16777216.0 * reference / gain
            : (code / 8388608.0 - 1) * reference / gain;
    }
    public string Validate(uint mode, uint config)
    {
        if ((mode & ~0xFCAFFFu) != 0 || (config & ~0xC0FFDFu) != 0) return "Hay bits reservados activados.";
        if ((mode & 1023) == 0) return "FS debe estar entre 1 y 1023.";
        if ((config & 0xff00) == 0) return "Selecciona al menos un canal.";
        if (Gains[config & 7] == 0) return "Ganancias 2 y 4 no son válidas para AD7195.";
        if ((config & 0x400) != 0 && (config & 15) != 0) return "Temperatura interna: usa ganancia 1 y bipolar para la conversión a °C.";
        if ((config & 0x400000) != 0 && (config & 0x800000) == 0) return "ACX requiere CHOP.";
        if ((config & 0x80) != 0 && ((config & 0x10) == 0 || (config & 0x800000) != 0)) return "Burnout requiere buffer y CHOP desactivado.";
        return "";
    }
    public static double NominalRate(uint mode, uint config, double clock = 4915200)
    {
        int fs = (int)(mode & 1023), order = (mode & 0x8000) != 0 ? 3 : 4;
        int channels = System.Numerics.BitOperations.PopCount((config >> 8) & 255);
        if (fs == 0 || channels == 0) return 0;
        double baseRate = clock / 1024 / fs;
        if ((config & 0x800000) != 0) return baseRate / order / (channels > 1 ? 2 : 1);
        return baseRate / (channels > 1 || (mode & 0x800) != 0 ? order : 1);
    }
    public static double Span(uint config, double reference) => reference / Gains[config & 7] * ((config & 8) != 0 ? 1 : 2);
}
