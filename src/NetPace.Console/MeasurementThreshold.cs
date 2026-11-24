using System.Globalization;

namespace NetPace.Console;

/// <summary>
/// A target one measurement of a run is checked against: what <c>--min-download</c>,
/// <c>--min-upload</c> and <c>--max-latency</c> each hold the run to.
/// </summary>
/// <remarks>
/// Kept in the measurement's own fixed unit - whole bits per second, or whole milliseconds - so the
/// comparison is between two whole numbers and no display option can move it. The unit the user
/// typed is kept only to write the figures back in: a target given in <c>Mbps</c> is reported in
/// <c>Mbps</c>, whatever <c>--unit</c> and its siblings say about the result itself.
/// </remarks>
public sealed record MeasurementThreshold
{
    // Matches what the library writes a speed to.
    private const int LeastDecimals = 2;

    // Enough that two figures a single fixed unit apart are still told apart in the largest unit
    // NetPace writes, a pebibyte per second.
    private const int MostDecimals = 16;

    private readonly decimal fixedUnitsPerUnit;

    /// <summary>
    /// Gets the target in the measurement's own fixed unit: whole bits per second for a speed,
    /// whole milliseconds for a latency.
    /// </summary>
    public required long Target { get; init; }

    /// <summary>
    /// Gets the unit the target was given in, spelled as NetPace writes it, for example
    /// <c>Mbps</c> or <c>ms</c>.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Gets how many of the measurement's fixed units one <see cref="Unit"/> is worth: a thousand
    /// for <c>Kbps</c>, eight thousand for <c>KBps</c>, one for <c>ms</c>. Never zero or less:
    /// every figure this type writes is divided by it.
    /// </summary>
    public required decimal FixedUnitsPerUnit
    {
        get => fixedUnitsPerUnit;
        init => fixedUnitsPerUnit = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "A unit must be worth at least one of the measurement's fixed units.");
    }

    /// <summary>
    /// Gets the target in the unit it was given in, for example <c>50 Mbps</c>.
    /// </summary>
    /// <remarks>
    /// Written to as many decimal places as it takes to state the target exactly, so a measured
    /// figure reported beside it can never read as the higher number: a 2.564 Mbps minimum is
    /// reported as <c>2.564 Mbps</c>, not rounded to the <c>2.56 Mbps</c> that a measurement of
    /// 2.563 Mbps would read above.
    /// </remarks>
    public string TargetFigure => Write(Target, MostDecimals);

    /// <summary>
    /// A measured figure written in the target's own unit, for example <c>0.01 Mbps</c>.
    /// </summary>
    /// <remarks>
    /// Carries however many decimal places it takes for the figure not to read as the target: a
    /// download of 49,999,999 bits per second against a 50 Mbps minimum is reported as
    /// <c>49.999999 Mbps</c> rather than rounded onto the very target it missed.
    /// </remarks>
    /// <param name="figure">The measured figure, in the measurement's own fixed unit.</param>
    public string Figure(long figure)
    {
        for (var decimals = LeastDecimals; decimals < MostDecimals; decimals++)
        {
            string written = Write(figure, decimals);
            if (written != Write(Target, decimals))
            {
                return written;
            }
        }

        return Write(figure, MostDecimals);
    }

    /// <summary>
    /// One figure in this target's unit, to the given number of decimal places, with trailing
    /// zeroes left off and through the invariant culture so a reported figure reads the same on
    /// every machine.
    /// </summary>
    private string Write(long figure, int decimals)
    {
        var format = "0." + new string('#', decimals);

        return (figure / FixedUnitsPerUnit).ToString(format, CultureInfo.InvariantCulture) + " " + Unit;
    }
}
