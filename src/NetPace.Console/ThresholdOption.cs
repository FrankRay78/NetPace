using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace NetPace.Console;

/// <summary>
/// Reading the value of a threshold option: a speed with its unit for <c>--min-download</c> and
/// <c>--min-upload</c>, a latency with its unit for <c>--max-latency</c>.
/// </summary>
/// <remarks>
/// The units accepted are the ones NetPace displays speeds in, listed here rather than in
/// <c>NetPace.Core</c>: how a command line is read is a CLI concern, and the library's own
/// suffixes are an output format with no parser of its own. The two lists are held together by a
/// test that reads every suffix the library writes back through this parser, so one list gaining a
/// unit the other does not have fails the suite.
/// <para>
/// A suffix is matched with the scale prefix in any case and the bits-or-bytes letter exactly, so
/// <c>50mbps</c> and <c>50Mbps</c> are the same 50 megabits while <c>50MBps</c> is 50 megabytes,
/// eight times as much. The target is rounded to a whole bit per second or a whole millisecond,
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
    /// What every speed suffix ends in. Its length locates the bits-or-bytes letter in front of
    /// it, which is the one character of a suffix whose case carries meaning.
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
    /// <remarks>
    /// A missing token means the option was not given at all, which is how the parser is asked for
    /// the absent default; nothing is reported for it.
    /// </remarks>
    public static MeasurementThreshold? ParseSpeed(ArgumentResult result, string optionName)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var value = result.Tokens[0].Value;
        if (TryParseSpeed(value, out MeasurementThreshold? threshold))
        {
            return threshold;
        }

        result.AddError($"'{value}' is not a valid value for {optionName}. Give a positive speed with its unit, such as {SpeedExample}.");
        return null;
    }

    /// <summary>
    /// Reads a <c>--max-latency</c> value, reporting an unreadable one against
    /// <paramref name="optionName"/> with a workable example.
    /// </summary>
    public static MeasurementThreshold? ParseLatency(ArgumentResult result, string optionName)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Tokens.Count == 0)
        {
            return null;
        }

        var value = result.Tokens[0].Value;
        if (TryParseLatency(value, out MeasurementThreshold? threshold))
        {
            return threshold;
        }

        result.AddError($"'{value}' is not a valid value for {optionName}. Give a positive latency in milliseconds, such as {LatencyExample}.");
        return null;
    }

    /// <summary>
    /// Reads a speed with its unit, such as <c>50Mbps</c> or <c>2.5MBps</c>.
    /// </summary>
    /// <returns><see langword="true"/> where the value named a positive speed in a unit NetPace writes.</returns>
    public static bool TryParseSpeed(string value, [NotNullWhen(true)] out MeasurementThreshold? threshold)
    {
        threshold = null;

        if (!TrySplit(value, out decimal figure, out string suffix))
        {
            return false;
        }

        foreach ((string candidate, decimal bitsPerUnit) in SpeedUnits)
        {
            if (!SuffixMatches(suffix, candidate))
            {
                continue;
            }

            return TryBuild(figure, bitsPerUnit, candidate, out threshold);
        }

        return false;
    }

    /// <summary>
    /// Reads a latency with its unit, such as <c>40ms</c>.
    /// </summary>
    /// <returns><see langword="true"/> where the value named a positive latency in milliseconds.</returns>
    public static bool TryParseLatency(string value, [NotNullWhen(true)] out MeasurementThreshold? threshold)
    {
        threshold = null;

        if (!TrySplit(value, out decimal figure, out string suffix))
        {
            return false;
        }

        if (!string.Equals(suffix, LatencyUnit, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return TryBuild(figure, 1m, LatencyUnit, out threshold);
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

        return units.ToArray();
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

        if (value is null)
        {
            return false;
        }

        var text = value.Trim();

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
    /// Whether a typed suffix names a known one: every character matched in either case, bar the
    /// bits-or-bytes letter, whose case is what tells <c>Mbps</c> from <c>MBps</c>.
    /// </summary>
    private static bool SuffixMatches(string typed, string known)
    {
        if (typed.Length != known.Length)
        {
            return false;
        }

        var unitLetter = known.Length - PerSecond.Length - 1;

        for (var index = 0; index < known.Length; index++)
        {
            var matches = index == unitLetter
                ? typed[index] == known[index]
                : char.ToUpperInvariant(typed[index]) == char.ToUpperInvariant(known[index]);

            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Builds the threshold, refusing a figure that does not come to at least one whole fixed unit
    /// or that no measurement could ever reach.
    /// </summary>
    private static bool TryBuild(decimal figure, decimal fixedUnitsPerUnit, string unit, [NotNullWhen(true)] out MeasurementThreshold? threshold)
    {
        threshold = null;

        if (figure > long.MaxValue / fixedUnitsPerUnit)
        {
            return false;
        }

        var target = Math.Round(figure * fixedUnitsPerUnit, MidpointRounding.AwayFromZero);
        if (target < 1)
        {
            return false;
        }

        threshold = new MeasurementThreshold
        {
            Target = (long)target,
            Unit = unit,
            FixedUnitsPerUnit = fixedUnitsPerUnit
        };

        return true;
    }
}
