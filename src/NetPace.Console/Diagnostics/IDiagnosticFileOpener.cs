using System.Text;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Opens the file <c>--diagnostics-file</c> names, so the records go there instead of to the error
/// stream.
/// </summary>
/// <remarks>
/// An abstraction over one <see cref="StreamWriter"/>, because a destination that fails part way
/// through a run is reported behaviour and no real path fails that way on demand everywhere.
/// </remarks>
public interface IDiagnosticFileOpener
{
    /// <summary>
    /// Opens the diagnostics file for writing, replacing whatever it held.
    /// </summary>
    /// <param name="path">The path the user named.</param>
    /// <returns>The writer the records are flushed to. The caller owns it and closes it.</returns>
    /// <remarks>
    /// An open that fails raises whatever the destination produced, so a caller catches by intent
    /// rather than by type.
    /// </remarks>
    TextWriter Open(string path);
}

/// <summary>
/// Opens the real file the user named, replacing whatever it held.
/// </summary>
/// <remarks>
/// Replaced rather than appended to, and <c>--file-mode</c> does not reach it: a diagnostics file
/// describes one run, so two runs' records would make its totals read as one.
/// <para>
/// UTF-8 with no byte-order mark, for the reason <see cref="FileConsole"/> states.
/// </para>
/// </remarks>
public sealed class DiagnosticFileOpener : IDiagnosticFileOpener
{
    private static readonly UTF8Encoding Utf8NoByteOrderMark = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public TextWriter Open(string path) => new StreamWriter(path, append: false, Utf8NoByteOrderMark);
}
