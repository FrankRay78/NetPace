using System.Text;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Opens the file <c>--diagnostics-file</c> names, so the records go there instead of to the error
/// stream.
/// </summary>
/// <remarks>
/// An abstraction over one <see cref="StreamWriter"/> because a destination that fails part way
/// through a run is behaviour NetPace reports, and no real path fails that way on demand on every
/// platform the suite runs on.
/// </remarks>
public interface IDiagnosticFileOpener
{
    /// <summary>
    /// Opens the diagnostics file for writing, replacing whatever it held.
    /// </summary>
    /// <param name="path">The path the user named.</param>
    /// <returns>The writer the records are flushed to. The caller owns it and closes it.</returns>
    /// <remarks>
    /// An open that fails raises, and the caller treats that as fatal. The type raised is whatever
    /// the destination produced, so a caller catches by intent rather than by type: a file path
    /// alone reaches here as <see cref="IOException"/> for a missing directory,
    /// <see cref="UnauthorizedAccessException"/> for a denied one or for a directory given as the
    /// path, and <see cref="ArgumentException"/> or <see cref="NotSupportedException"/> for a path
    /// the platform will not take.
    /// </remarks>
    TextWriter Open(string path);
}

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
