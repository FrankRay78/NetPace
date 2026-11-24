using System.Globalization;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Formats one diagnostic record as a single logfmt line.
/// </summary>
/// <remarks>
/// Every line is one record, in a fixed field order. Repeating the context a record needs to stand
/// alone is the point: a grepped subset stays meaningful without the records around it.
/// <para>
/// No column padding, deliberately: one long URL would reflow a whole block in a snapshot diff.
/// </para>
/// </remarks>
public static class DiagnosticRecord
{
    /// <summary>
    /// The timestamp format. Deliberately its own, carrying milliseconds the result timestamps do
    /// not: a diagnostic stream is read to see the order and spacing of events within a single run.
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
    /// Newlines, tabs and backslashes are always escaped: a raw newline would split one record
    /// into two meaningless lines, and a trailing backslash would close its own quote.
    /// <para>
    /// The backslash pass must stay first, or a later pass doubles the escape it just added.
    /// </para>
    /// <para>
    /// An escaped value is always quoted, logfmt readers processing escapes only inside quotes.
    /// </para>
    /// </remarks>
    private static string FormatValue(string value)
    {
        string escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

        // A quote in the escaped value can only have come from one in the original, which the
        // inequality below has already caught - so there is no separate check for it.
        bool needsQuoting = escaped.Length == 0 ||
            !string.Equals(escaped, value, StringComparison.Ordinal) ||
            escaped.Contains(' ', StringComparison.Ordinal) ||
            escaped.Contains('=', StringComparison.Ordinal);

        return needsQuoting ? $"\"{escaped}\"" : escaped;
    }
}
