namespace NetPace.Core;

public static class SpeedTestResultExtensions
{
    private static readonly string[] SI_BitUnits = { "bps", "Kbps", "Mbps", "Gbps", "Tbps", "Pbps" };
    private static readonly string[] SI_ByteUnits = { "Bps", "KBps", "MBps", "GBps", "TBps", "PBps" };

    private static readonly string[] IEC_BitUnits = { "bps", "Kibps", "Mibps", "Gibps", "Tibps", "Pibps" };
    private static readonly string[] IEC_ByteUnits = { "Bps", "KiBps", "MiBps", "GiBps", "TiBps", "PiBps" };

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        var isBits = unit == SpeedUnit.BitsPerSecond;
        double divisor = unitSystem == SpeedUnitSystem.IEC ? 1024 : 1000;

        var speed = isBits
            ? result.BytesProcessed * 8.0 / ((double)result.ElapsedMilliseconds / 1000)
            : result.BytesProcessed / ((double)result.ElapsedMilliseconds / 1000);

        if (scale == SpeedScale.Auto)
        {
            return FormatSpeed(speed, isBits, unitSystem, divisor);
        }
        else
        {
            return FormatSpeedWithFixedScale(speed, isBits, unitSystem, divisor, (int)scale - 1);
        }
    }

    private static string FormatSpeed(double speed, bool isBits, SpeedUnitSystem unitSystem, double divisor)
    {
        var units = isBits
            ? unitSystem == SpeedUnitSystem.IEC ? IEC_BitUnits : SI_BitUnits
            : unitSystem == SpeedUnitSystem.IEC ? IEC_ByteUnits : SI_ByteUnits;

        var index = 0;
        while (Math.Round(speed, 2) >= divisor && index < units.Length - 1)
        {
            speed /= divisor;
            index++;
        }

        return $"{speed.ToString("0.##")} {units[index]}";
    }

    private static string FormatSpeedWithFixedScale(double speed, bool isBits, SpeedUnitSystem unitSystem, double divisor, int fixedScale)
    {
        var units = isBits
            ? unitSystem == SpeedUnitSystem.IEC ? IEC_BitUnits : SI_BitUnits
            : unitSystem == SpeedUnitSystem.IEC ? IEC_ByteUnits : SI_ByteUnits;

        // Select the correct scale index (Base=0, Kilo=1, Mega=2, etc).
        var index = Math.Min(Math.Max(0, fixedScale), units.Length - 1);
        
        // Apply the fixed scaling.
        for (int i = 0; i < index; i++)
        {
            speed /= divisor;
        }

        return $"{speed.ToString("0.##")} {units[index]}";
    }
}