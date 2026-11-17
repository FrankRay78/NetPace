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
    /// <exception cref="IOException">The path could not be opened for writing.</exception>
    TextWriter Open(string path);
}
