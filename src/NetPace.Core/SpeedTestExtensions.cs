using System.Globalization;

namespace NetPace.Core;

/// <summary>
/// Extension methods for <see cref="SpeedTestResult"/> and <see cref="SpeedTestProgress"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every speed string these methods return is formatted through the invariant culture: <c>.</c> as
/// the decimal separator and no digit grouping. A consumer therefore reads the same text whatever
/// culture the calling thread carries, so a formatted speed is safe to log, compare and parse
/// across machines.
/// </para>
/// <para>
/// A speed is written to three significant figures, which is as much precision as a speed test
/// supports: run-to-run variance is several percent, so a fourth figure would be noise. The
/// decimal places follow the figure's own size - below 10 two places (<c>8.00</c>), from 10 to
/// below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>) - so no
/// auto-scaled reading is ever wider than four characters. Rounding never leaves a figure in the wrong band
/// or unit: <c>9.996</c> reads <c>10.0</c>, <c>99.96</c> reads <c>100</c>, and under auto-scaling a
/// speed that rounds up to its unit system's divisor is written as <c>1.00</c> of the next unit
/// rather than as <c>1000 Kbps</c>. The three figures are only guaranteed under auto-scaling. Digits before
/// the decimal point are never rounded away, so a speed pinned to a small unit with
/// <see cref="SpeedScale"/> keeps every one of them (<c>125000 Kbps</c>), while one pinned to a
/// large unit keeps two decimal places and so can read <c>0.00 Gbps</c> without being zero. A zero
/// speed reads as a bare <c>0</c>. For the measurement without this rounding, read
/// <see cref="GetBitsPerSecond"/>.
/// </para>
/// </remarks>
public static class SpeedTestExtensions
{
    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(result);

        return GetSpeedString(result, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
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
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(result);

        return GetSpeedStringParts(result, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(result);

        return FormatSpeedParts(result.BytesProcessed, result.ElapsedMilliseconds, unit, unitSystem, scale);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static string GetSpeedString(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return GetSpeedString(progress, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static string GetSpeedString(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return FormatSpeed(progress, unit, unitSystem, scale);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return GetSpeedStringParts(progress, unit, unitSystem, SpeedScale.Auto);
    }

    /// <summary>
    /// Calculates and formats the speed string, to the three significant figures
    /// <see cref="SpeedTestExtensions"/> describes: below 10 two decimal places (<c>8.00</c>), from
    /// 10 to below 100 one place (<c>18.7</c>), and from 100 up a whole number (<c>245</c>).
    /// </summary>
    public static (string speed, string unit) GetSpeedStringParts(this SpeedTestProgress progress, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
    {
        ArgumentNullException.ThrowIfNull(progress);

        return FormatSpeedParts(progress.BytesProcessed, progress.ElapsedMilliseconds, unit, unitSystem, scale);
    }

    #region Private Methods

    // Indexed by the decimal places a band allows, and formatted through the invariant culture so
    // a CSV row cannot gain a field from a comma-decimal thread.
    private static readonly string[] FigureFormats = ["0", "0.0", "0.00"];

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

        // Measured against the figure as written, so a speed rounding up to the divisor steps into
        // the next unit rather than being drawn as 1000 of this one.
        var index = 0;
        while (Figure(speed).Value >= divisor && index < units.Length - 1)
        {
            speed /= divisor;
            index++;
        }

        return (FigureText(speed), units[index]);
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

        return (FigureText(speed), units[index]);
    }

    /// <summary>
    /// How many decimal places a figure of this size is written to, so that it carries three
    /// significant figures where the figure is below 1000.
    /// </summary>
    private static int DecimalPlacesFor(double figure) => figure switch
    {
        < 10 => 2,
        < 100 => 1,
        _ => 0,
    };

    /// <summary>
    /// The figure <paramref name="speed"/> will be written as, and the decimal places it takes.
    /// </summary>
    /// <remarks>
    /// Rounding can carry a figure into the band above, which allows one fewer decimal place, so
    /// the band is read again off the rounded figure and the speed rounded afresh.
    /// <para>
    /// A second pass is enough, a figure being able to cross only one band boundary by rounding.
    /// </para>
    /// </remarks>
    private static (double Value, int DecimalPlaces) Figure(double speed)
    {
        var places = DecimalPlacesFor(speed);
        var rounded = Math.Round(speed, places, MidpointRounding.AwayFromZero);
        var placesAfterRounding = DecimalPlacesFor(rounded);

        if (placesAfterRounding == places)
        {
            return (rounded, places);
        }

        return (Math.Round(speed, placesAfterRounding, MidpointRounding.AwayFromZero), placesAfterRounding);
    }

    /// <summary>
    /// <paramref name="speed"/> written to three significant figures, with no unit beside it.
    /// </summary>
    private static string FigureText(double speed)
    {
        // A measurement of nothing, as when no byte arrived: a bare zero rather than 0.00, because there is no measurement to show three figures of.
        if (speed == 0)
        {
            return "0";
        }

        (double value, int places) = Figure(speed);

        return value.ToString(FigureFormats[places], CultureInfo.InvariantCulture);
    }

    #endregion
}
