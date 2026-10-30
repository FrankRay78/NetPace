using Microsoft.VisualBasic.FileIO;

namespace NetPace.Console.Tests;

/// <summary>
/// Reads CSV output the way a standard CSV consumer would, so a test asserts what a reader recovers
/// rather than the text NetPace happened to write.
/// </summary>
internal static class CsvOutput
{
    /// <summary>
    /// Parses CSV output with <c>TextFieldParser</c>, a quoted-field CSV reader from the shared
    /// framework. Whitespace is kept: it trims inside quoted fields by default, which would discard
    /// the space the writer substitutes for a line break.
    /// </summary>
    public static string[][] Records(string output)
    {
        using var reader = new StringReader(output);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        var records = new List<string[]>();
        while (!parser.EndOfData)
        {
            records.Add(parser.ReadFields()!);
        }

        return [.. records];
    }

    /// <summary>
    /// Reads the named column out of the single data row of parsed records, so a test names the
    /// column it means rather than counting fields. Where a column name appears more than once —
    /// which a custom field may cause — the first is read.
    /// </summary>
    public static string Field(string[][] records, string column)
    {
        int index = Array.IndexOf(records[0], column);
        Assert.True(index >= 0, $"The CSV header does not carry a '{column}' column: {string.Join(',', records[0])}");
        return records[1][index];
    }
}
