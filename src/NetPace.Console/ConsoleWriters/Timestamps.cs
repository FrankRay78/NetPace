namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The timestamp formats the result output uses — one per kind of reader, neither of them a user
/// choice, so a script never has to know which format a given run picked. Both are rendered through
/// the invariant culture, so the calendar is Gregorian, the digits ASCII, and the separators fixed,
/// whatever regional settings the machine carries.
/// </summary>
internal static class Timestamps
{
    /// <summary>
    /// Local time as a person reads it, in the layout the default and minimal output have always
    /// used. No offset: someone reading their own terminal already knows the zone they are in.
    /// </summary>
    internal const string HumanReadable = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// ISO 8601 local time followed by its offset from UTC, for the CSV output. The offset is what
    /// makes the timestamp one unambiguous instant, so rows written on machines in different zones
    /// — or either side of a clock change — sort and compare directly, and any date parser reads
    /// them without being told the format. It is the shape System.Text.Json writes a
    /// <see cref="DateTimeOffset"/> in, which is how the JSON output reaches the same text.
    /// </summary>
    internal const string MachineReadable = "yyyy-MM-ddTHH:mm:sszzz";

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
