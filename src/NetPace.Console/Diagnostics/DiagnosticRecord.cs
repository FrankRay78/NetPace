using System.Globalization;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Formats one diagnostic record as a single logfmt line.
/// </summary>
/// <remarks>
/// <para>
/// Every line is one record: <c>ts</c> first, <c>event</c> second, then the fields the event
/// carries, in a fixed order, separated by exactly one space. Repeating the context a record needs
/// to stand alone - <c>ts</c> on every record, and <c>test</c> on each one belonging to a test - is
/// the point of the format: a grepped subset stays meaningful without the records around it, which
/// makes <c>grep status=failed</c> the whole triage workflow. Run-level records
/// (<c>run.start</c>, <c>run.invocation</c>, <c>server.selected</c>, <c>run.end</c>) belong to no
/// test and carry no <c>test</c> field.
/// </para>
/// <para>
/// There is no column padding, deliberately. Padded columns would make one long URL reflow a whole
/// block in a snapshot diff, burying the real change - the same reason the codebase avoids aligned
/// whitespace in source.
/// </para>
/// </remarks>
public static class DiagnosticRecord
{
    /// <summary>
    /// The timestamp format. Deliberately not affected by <c>--datetimeformat</c>, which shapes the
    /// result: diagnostics stay stable however a user has chosen to format their output.
    /// </summary>
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff";

    /// <summary>
    /// Formats one record: <c>ts</c> first, <c>event</c> second, then the given fields in order. A
    /// field whose value is <see langword="null"/> is omitted.
    /// </summary>
    public static string Format(DateTime timestamp, string eventName, params (string Key, string? Value)[] fields)
    {
        var record = new StringBuilder();

        record.Append("ts=").Append(timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture));
        record.Append(" event=").Append(eventName);

        foreach ((string key, string? value) in fields)
        {
            if (value is null)
            {
                continue;
            }

            record.Append(' ').Append(key).Append('=').Append(FormatValue(value));
        }

        return record.ToString();
    }

    /// <summary>
    /// A value, bare unless it needs quoting.
    /// </summary>
    /// <remarks>
    /// Newlines and carriage returns are escaped whatever the value looks like, because exception
    /// messages are frequently multi-line and a raw newline mid-record would split one record into
    /// two lines that each mean nothing. Backslashes are escaped too, so a value ending in one
    /// cannot close its own quote with <c>\"</c> and swallow the rest of the line.
    /// <para>
    /// The backslash pass must stay first. Escaping quotes first turns <c>a"b</c> into
    /// <c>a\"b</c>, and a later backslash pass then doubles that escape into <c>a\\"b</c>,
    /// breaking the very quote it was escaping. Order the other way and each pass sees only
    /// characters the other has not touched.
    /// </para>
    /// <para>
    /// A value that needed escaping is always quoted, even when it carries no space. logfmt readers
    /// process escapes only inside quotes, so a bare <c>C:\\x</c> would be read back with the
    /// doubled separator it never had.
    /// </para>
    /// </remarks>
    private static string FormatValue(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

        bool needsQuoting = escaped.Length == 0 ||
            !string.Equals(escaped, value, StringComparison.Ordinal) ||
            escaped.Contains(' ', StringComparison.Ordinal) ||
            escaped.Contains('=', StringComparison.Ordinal) ||
            escaped.Contains('"', StringComparison.Ordinal);

        return needsQuoting ? $"\"{escaped}\"" : escaped;
    }
}
