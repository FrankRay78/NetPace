namespace NetPace.Console.Diagnostics;

/// <summary>
/// Where diagnostic records go: the process error stream in production, a buffer in tests.
/// </summary>
/// <remarks>
/// A plain <see cref="TextWriter"/> on purpose: a Spectre console wraps at terminal width and
/// parses markup, either of which would break the one-record-per-line contract.
/// </remarks>
public sealed class DiagnosticOutput(TextWriter writer)
{
    /// <summary>
    /// Gets the writer diagnostic records are flushed to.
    /// </summary>
    public TextWriter Writer { get; } = writer;
}
