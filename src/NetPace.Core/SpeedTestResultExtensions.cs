namespace NetPace.Core;

public static class SpeedTestResultExtensions
{
    private static readonly string[] SI_BitUnits = { "bps", "Kbps", "Mbps", "Gbps", "Tbps", "Pbps" };
    private static readonly string[] SI_ByteUnits = { "Bps", "KBps", "MBps", "GBps", "TBps", "PBps" };

    private static readonly string[] IEC_BitUnits = { "bps", "Kibps", "Mibps", "Gibps", "Tibps", "Pibps" };
    private static readonly string[] IEC_ByteUnits = { "Bps", "KiBps", "MiBps", "GiBps", "TiBps", "PiBps" };

    /// <summary>
    /// Calculates and formats the speed string based on the given speed unit and unit system.
    /// </summary>
    public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        var isBits = unit == SpeedUnit.BitsPerSecond;
        double divisor = unitSystem == SpeedUnitSystem.IEC ? 1024 : 1000;

        var speed = isBits
            ? result.BytesProcessed * 8 / ((double)result.ElapsedMilliseconds / 1000)
            : result.BytesProcessed / ((double)result.ElapsedMilliseconds / 1000);

        return FormatSpeed(speed, isBits, unitSystem, divisor);
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
}