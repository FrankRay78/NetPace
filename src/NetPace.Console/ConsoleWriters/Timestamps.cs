using System.Globalization;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The timestamp formats the result output uses — one per kind of reader, neither of them a user
/// choice, so a script never has to know which format a given run picked. Rendering lives here
/// rather than at each writer, so the invariant culture cannot be passed at one call site and
/// forgotten at the next: the calendar is Gregorian, the digits ASCII, and the separators fixed,
/// whatever regional settings the machine carries.
/// </summary>
internal static class Timestamps
{
    /// <summary>
    /// Local time as a person reads it, in the layout the default and minimal output have always
    /// defaulted to. No offset: someone reading their own terminal already knows the zone they are
    /// in.
    /// </summary>
    private const string HumanReadableFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// ISO 8601 local time followed by its offset from UTC, for the CSV output. The offset is what
    /// makes the timestamp one unambiguous instant, so rows written on machines in different zones
    /// — or either side of a clock change — sort and compare directly, and any date parser reads
    /// them without being told the format. It is the shape System.Text.Json writes a
    /// <see cref="DateTimeOffset"/> in, which is how the JSON output reaches the same text.
    /// </summary>
    private const string MachineReadableFormat = "yyyy-MM-ddTHH:mm:sszzz";

    /// <summary>
    /// Every character a machine-readable timestamp can contain: the digits, the date and time
    /// separators, the <c>T</c> between them, and either sign of the UTC offset. The CSV writer
    /// quotes nothing, so a delimiter drawn from this set would split the timestamp across two
    /// fields and leave the row with one more field than its header — which is why
    /// <c>--csv-delimiter</c> refuses them.
    /// </summary>
    internal const string MachineReadableCharacters = "0123456789-T:+";

    /// <summary>
    /// Renders the instant for a person to read, as the default and minimal output do.
    /// </summary>
    internal static string ToHumanReadable(this DateTimeOffset value)
    {
        return value.ToString(HumanReadableFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Renders the instant for a program to read, as the CSV output does. The format carries no
    /// fractional seconds, so it drops any the clock reported — which is why the JSON output,
    /// serialized by System.Text.Json rather than through this format, has to call
    /// <see cref="ToWholeSecond"/> to arrive at the same text.
    /// </summary>
    internal static string ToMachineReadable(this DateTimeOffset value)
    {
        return value.ToString(MachineReadableFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Drops any fraction of a second, so the machine-readable timestamp is a whole number of
    /// seconds whichever writer renders it. Without this, System.Text.Json would carry the clock's
    /// sub-second precision into the JSON output while the CSV format string silently dropped it,
    /// and the two would disagree about the same instant.
    /// </summary>
    internal static DateTimeOffset ToWholeSecond(this DateTimeOffset value)
    {
        return value.AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
    }
}
