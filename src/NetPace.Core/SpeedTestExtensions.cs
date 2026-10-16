using System.Globalization;

namespace NetPace.Core;

/// <summary>
/// Extension methods for <see cref="SpeedTestResult"/> and <see cref="SpeedTestProgress"/>.
/// </summary>
public static class SpeedTestExtensions
{
    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(result);

        return GetSpeedString(result, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(result);

        return FormatSpeed(result, unit, unitSystem, scale);
    }

    /// <summary>
    /// Calculates the speed in bits per second, rounded to the nearest whole number.
    /// </summary>
    /// <remarks>
    /// The figure does not depend on <see cref="SpeedUnit"/>, <see cref="SpeedUnitSystem"/> or
    /// <see cref="SpeedScale"/>: those shape how a speed is displayed, and this is the measurement
    /// itself in a fixed unit, so a machine-readable consumer reads the same number for the same
    /// measurement whatever a caller's display preferences are. A test that moved no bytes, or in
    /// which no time elapsed, reports zero.
    /// </remarks>
    public static long GetBitsPerSecond(this SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var bitsPerSecond = CalculateBitsPerSecond(result.BytesProcessed, result.ElapsedMilliseconds);

        return (long)Math.Round(bitsPerSecond, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(result);

        return GetSpeedStringParts(result, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(result);

        return FormatSpeedParts(result.BytesProcessed, result.ElapsedMilliseconds, unit, unitSystem, scale);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static string GetSpeedString(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return GetSpeedString(progress, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static string GetSpeedString(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return FormatSpeed(progress, unit, unitSystem, scale);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return GetSpeedStringParts(progress, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string.
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return FormatSpeedParts(progress.BytesProcessed, progress.ElapsedMilliseconds, unit, unitSystem, scale);
    }

    #region Private Methods

    // Speeds are written the same way on every machine: "." as the decimal separator, at
    // most two decimal places, and no digit grouping. Formatted through the invariant
    // culture rather than the caller's, so a consumer on a comma-decimal thread reads the
    // same text as everyone else, and a CSV row cannot gain a field from a stray comma.
    private const string SpeedFormat = "0.##";

    private const double BitsPerByte = 8.0;
    private const double MillisecondsPerSecond = 1000.0;

    private static readonly string[] SI_BitUnits = { "bps", "Kbps", "Mbps", "Gbps", "Tbps", "Pbps" };
    private static readonly string[] SI_ByteUnits = { "Bps", "KBps", "MBps", "GBps", "TBps", "PBps" };

    private static readonly string[] IEC_BitUnits = { "bps", "Kibps", "Mibps", "Gibps", "Tibps", "Pibps" };
    private static readonly string[] IEC_ByteUnits = { "Bps", "KiBps", "MiBps", "GiBps", "TiBps", "PiBps" };

    private static string FormatSpeed(SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale)
    {
        (string speed, string unit) formattedParts = FormatSpeedParts(result.BytesProcessed, result.ElapsedMilliseconds, unit, unitSystem, scale);

        return formattedParts.speed + " " + formattedParts.unit;
    }

    private static string FormatSpeed(SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale)
    {
        (string speed, string unit) formattedParts = FormatSpeedParts(progress.BytesProcessed, progress.ElapsedMilliseconds, unit, unitSystem, scale);

        return formattedParts.speed + " " + formattedParts.unit;
    }

    private static (string speed, string unit) FormatSpeedParts(long bytesProcessed, long elapsedMilliseconds, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale)
    {
        var isBits = unit == SpeedUnit.BitsPerSecond;
        double divisor = unitSystem == SpeedUnitSystem.IEC ? 1024 : 1000;

        // No time elapsed means no speed measured, reported at the base unit without scaling.
        if (elapsedMilliseconds == 0)
        {
            var baseUnit = isBits ? "bps" : "Bps";
            return ("0", baseUnit);
        }

        var bitsPerSecond = CalculateBitsPerSecond(bytesProcessed, elapsedMilliseconds);
        var speed = isBits ? bitsPerSecond : bitsPerSecond / BitsPerByte;

        (string speed, string unit) formattedResult;

        if (scale == SpeedScale.Auto)
        {
            formattedResult = FormatSpeed_AutoScale(speed, isBits, unitSystem, divisor);
        }
        else
        {
            formattedResult = FormatSpeed_FixedScale(speed, isBits, unitSystem, divisor, (int)scale - 1);
        }

        return formattedResult;
    }

    // The unrounded figure. FormatSpeedParts and GetBitsPerSecond both derive from it, so the
    // formatted and numeric values can never drift apart.
    private static double CalculateBitsPerSecond(long bytesProcessed, long elapsedMilliseconds)
    {
        // Handle division by zero: no time elapsed means no speed measured
        if (elapsedMilliseconds == 0)
        {
            return 0;
        }

        return bytesProcessed * BitsPerByte / (elapsedMilliseconds / MillisecondsPerSecond);
    }

    private static (string speed, string unit) FormatSpeed_AutoScale(double speed, bool isBits, SpeedUnitSystem unitSystem, double divisor)
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

        return (speed.ToString(SpeedFormat, CultureInfo.InvariantCulture), units[index]);
    }

    private static (string speed, string unit) FormatSpeed_FixedScale(double speed, bool isBits, SpeedUnitSystem unitSystem, double divisor, int fixedScale)
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

        return (speed.ToString(SpeedFormat, CultureInfo.InvariantCulture), units[index]);
    }

    #endregion
}
