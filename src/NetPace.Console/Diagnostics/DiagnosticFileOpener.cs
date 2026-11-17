using System.Text;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Opens the real file the user named, replacing whatever it held.
/// </summary>
/// <remarks>
/// Replaced rather than appended to, and <c>--file-mode</c> does not reach it: a diagnostics file
/// describes one run, so a second run's records behind a first run's would make the totals in it
/// read as one. UTF-8 with no byte-order mark, for the reason <see cref="FileConsole"/> states:
/// nothing stands in front of the first record, so a Unix text tool reads it as a record.
/// </remarks>
public sealed class DiagnosticFileOpener : IDiagnosticFileOpener
{
    private static readonly UTF8Encoding Utf8NoByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public TextWriter Open(string path) => new StreamWriter(path, append: false, Utf8NoByteOrderMark);
}
