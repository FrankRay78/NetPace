using System.CommandLine.Parsing;
using System.Globalization;

namespace NetPace.Console;

/// <summary>
/// Reading the value of a threshold option: a speed with its unit for <c>--min-download</c> and
/// <c>--min-upload</c>, a latency with its unit for <c>--max-latency</c>.
/// </summary>
/// <remarks>
/// The units accepted are the ones NetPace displays speeds in, listed here rather than in
/// <c>NetPace.Core</c>: how a command line is read is a CLI concern, and the library's own
/// suffixes are an output format with no parser of its own. A test reads every suffix the library
/// writes back through this parser, so a unit the library gains or renames shows up as a threshold
/// value the CLI refuses.
/// <para>
/// A suffix is matched with the scale prefix in any case and the rest of it exactly, so
/// <c>50mbps</c> and <c>50Mbps</c> are the same 50 megabits while <c>50MBps</c> is 50 megabytes,
/// eight times as much. A spelling that folds the bits-or-bytes letter away, such as
/// <c>50MBPS</c>, is refused: it could be either, and reading it as the eight-times target would
/// be the costlier guess. The target is rounded to a whole bit per second or a whole millisecond,
/// which is the unit the measurement itself reports in.
/// </para>
/// </remarks>
internal static class ThresholdOption
{
    /// <summary>
    /// The speed a refused <c>--min-download</c> or <c>--min-upload</c> value is shown as a
    /// workable example.
    /// </summary>
    public const string SpeedExample = "50Mbps";

    /// <summary>
    /// The latency a refused <c>--max-latency</c> value is shown as a workable example.
    /// </summary>
    public const string LatencyExample = "40ms";

    /// <summary>
    /// The one unit a latency threshold takes. Milliseconds is what every output format reports a
    /// latency in, so there is no second spelling to accept.
    /// </summary>
    private const string LatencyUnit = "ms";

    /// <summary>
    /// What every speed suffix ends in, spelled as NetPace writes it. Its length locates the
    /// bits-or-bytes letter in front of it.
    /// </summary>
    private const string PerSecond = "ps";

    private const string BitsLetter = "b";
    private const string BytesLetter = "B";

    private const int BitsPerByte = 8;
    private const decimal SIStep = 1000m;
    private const decimal IECStep = 1024m;

    /// <summary>The scale prefixes of the SI units, smallest first.</summary>
    private static readonly string[] SIPrefixes = ["", "K", "M", "G", "T", "P"];

    /// <summary>The scale prefixes of the IEC units, in the same order.</summary>
    private static readonly string[] IECPrefixes = ["", "Ki", "Mi", "Gi", "Ti", "Pi"];

    /// <summary>
    /// Every speed suffix a threshold accepts, with how many bits per second one of it is worth.
    /// The base suffixes are shared by the two unit systems, so the list holds a few duplicates,
    /// which agree on their value and are therefore harmless to match in order.
    /// </summary>
    private static readonly (string Suffix, decimal BitsPerUnit)[] SpeedUnits = BuildSpeedUnits();

    /// <summary>
    /// Reads a <c>--min-download</c> or <c>--min-upload</c> value, reporting an unreadable one
    /// against <paramref name="optionName"/> with a workable example.
    /// </summary>
    public static MeasurementThreshold? ParseSpeed(ArgumentResult result, string optionName) =>
        Parse(result, optionName, ReadSpeed, $"Give a positive speed with its unit, such as {SpeedExample}, where b is bits and B is bytes.");

    /// <summary>
    /// Reads a <c>--max-latency</c> value, reporting an unreadable one against
    /// <paramref name="optionName"/> with a workable example.
    /// </summary>
    public static MeasurementThreshold? ParseLatency(ArgumentResult result, string optionName) =>
        Parse(result, optionName, ReadLatency, $"Give a positive latency in milliseconds, such as {LatencyExample}.");

    /// <summary>
    /// Reads one threshold value, reporting an unreadable one against <paramref name="optionName"/>
    /// with the <paramref name="advice"/> that names a workable example.
    /// </summary>
    /// <remarks>
    /// A missing token means the option was not given at all, which is how the parser is asked for
    /// the absent default; nothing is reported for it.
    /// </remarks>
    private static MeasurementThreshold? Parse(ArgumentResult result, string optionName, Func<string, MeasurementThreshold?> read, string advice)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var value = result.Tokens[0].Value;
        MeasurementThreshold? threshold = read(value);

        if (threshold is null)
        {
            result.AddError($"'{value}' is not a valid value for {optionName}. {advice}");
        }

        return threshold;
    }

    /// <summary>
    /// Reads a speed with its unit, such as <c>50Mbps</c> or <c>2.5MBps</c>, or
    /// <see langword="null"/> where the value named no positive speed in a unit NetPace writes.
    /// </summary>
    private static MeasurementThreshold? ReadSpeed(string value)
    {
        if (!TrySplit(value, out decimal figure, out string suffix))
        {
            return null;
        }

        foreach ((string candidate, decimal bitsPerUnit) in SpeedUnits)
        {
            if (SuffixMatches(suffix, candidate))
            {
                return Build(figure, bitsPerUnit, candidate, Math.Ceiling);
            }
        }

        return null;
    }

    /// <summary>
    /// Reads a latency with its unit, such as <c>40ms</c>, or <see langword="null"/> where the
    /// value named no positive latency in milliseconds.
    /// </summary>
    private static MeasurementThreshold? ReadLatency(string value)
    {
        if (!TrySplit(value, out decimal figure, out string suffix))
        {
            return null;
        }

        if (!string.Equals(suffix, LatencyUnit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Build(figure, 1m, LatencyUnit, Math.Floor);
    }

    /// <summary>
    /// Every suffix NetPace writes a speed in: the two unit systems, each in bits and in bytes, at
    /// every scale.
    /// </summary>
    private static (string Suffix, decimal BitsPerUnit)[] BuildSpeedUnits()
    {
        var units = new List<(string Suffix, decimal BitsPerUnit)>();

        AddScales(units, SIPrefixes, SIStep);
        AddScales(units, IECPrefixes, IECStep);

        return [.. units];
    }

    /// <summary>
    /// Adds one unit system's suffixes, in bits and in bytes, at every scale its prefixes name.
    /// </summary>
    private static void AddScales(List<(string Suffix, decimal BitsPerUnit)> units, string[] prefixes, decimal step)
    {
        var scale = 1m;

        foreach (string prefix in prefixes)
        {
            units.Add((prefix + BitsLetter + PerSecond, scale));
            units.Add((prefix + BytesLetter + PerSecond, scale * BitsPerByte));

            scale *= step;
        }
    }

    /// <summary>
    /// Splits a threshold value into its figure and its unit, accepting whitespace at either end
    /// and between the two.
    /// </summary>
    /// <remarks>
    /// The figure is read through the invariant culture, matching the way every formatted figure
    /// NetPace writes is composed: <c>2.5</c> is two and a half whatever the machine's locale, and
    /// <c>2,5</c> is not a figure at all.
    /// </remarks>
    private static bool TrySplit(string value, out decimal figure, out string suffix)
    {
        figure = 0;
        suffix = string.Empty;

        string text = value.Trim();

        var end = 0;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || text[end] == '.'))
        {
            end++;
        }

        suffix = text[end..].Trim();

        return decimal.TryParse(text[..end], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out figure)
            && figure > 0;
    }

    /// <summary>
    /// Whether a typed suffix names a known one: the scale prefix matched in either case, and the
    /// bits-or-bytes letter and the <c>ps</c> after it matched exactly. The letter's case is what
    /// tells <c>Mbps</c> from <c>MBps</c>, so a spelling that folds it away, such as
    /// <c>MBPS</c>, names neither and is refused rather than read as one of them.
    /// </summary>
    private static bool SuffixMatches(string typed, string known)
    {
        if (typed.Length != known.Length)
        {
            return false;
        }

        var unitLetter = known.Length - PerSecond.Length - 1;

        return typed[unitLetter] == known[unitLetter]
            && string.Equals(typed[..unitLetter], known[..unitLetter], StringComparison.OrdinalIgnoreCase)
            && string.Equals(typed[(unitLetter + 1)..], known[(unitLetter + 1)..], StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the threshold, refusing a figure that does not come to at least one whole fixed unit
    /// or that no measurement could ever reach.
    /// </summary>
    /// <param name="towards">
    /// Which way a fraction of a fixed unit is rounded: up for a minimum and down for a maximum.
    /// Every measured figure is a whole number of fixed units, so rounding the target away from
    /// the side it guards keeps the check exact: a 99.5 ms maximum misses a 100 ms latency, where
    /// rounding to the nearest unit would have met it.
    /// </param>
    private static MeasurementThreshold? Build(decimal figure, decimal fixedUnitsPerUnit, string unit, Func<decimal, decimal> towards)
    {
        if (figure > long.MaxValue / fixedUnitsPerUnit)
        {
            return null;
        }

        decimal target = towards(figure * fixedUnitsPerUnit);
        if (target < 1)
        {
            return null;
        }

        return new MeasurementThreshold
        {
            Target = (long)target,
            Unit = unit,
            FixedUnitsPerUnit = fixedUnitsPerUnit
        };
    }
}
