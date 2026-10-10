namespace NetPace.Console.Diagnostics;

/// <summary>
/// Where diagnostic records go: the process error stream in production, a buffer in tests.
/// </summary>
/// <remarks>
/// A plain <see cref="TextWriter"/> rather than an <see cref="IAnsiConsole"/> on purpose. A Spectre
/// console wraps at terminal width and parses markup, either of which would break the
/// one-record-per-line contract that makes a diagnostic log greppable - a single Ookla URL exceeds
/// 80 columns, and a <c>[</c> in an exception message is markup to Spectre.
/// </remarks>
public sealed class DiagnosticOutput(TextWriter writer)
{
    /// <summary>
    /// Gets the writer diagnostic records are flushed to.
    /// </summary>
    public TextWriter Writer { get; } = writer;
}
