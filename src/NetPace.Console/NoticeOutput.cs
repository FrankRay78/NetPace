namespace NetPace.Console;

/// <summary>
/// Where a word to the user about the way NetPace was asked to run goes: the process error stream
/// in production, a buffer in tests.
/// </summary>
/// <remarks>
/// A notice is not the result and is not a diagnostic. It goes to the error stream so a run whose
/// result is being read by another program — piped, redirected, or written with <c>--csv</c> or
/// <c>--json</c> — is never handed a sentence of English in the middle of its data, and it keeps
/// its own writer so a test can assert which stream carried what.
/// <para>
/// A plain <see cref="TextWriter"/> rather than an <see cref="IAnsiConsole"/>, for the same reason
/// <see cref="Diagnostics.DiagnosticOutput"/> is: the message is one line whatever the terminal
/// width, and a <c>[</c> in it would be markup to Spectre.Console.
/// </para>
/// </remarks>
public sealed class NoticeOutput(TextWriter writer)
{
    /// <summary>
    /// Gets the writer notices are written to.
    /// </summary>
    public TextWriter Writer { get; } = writer;
}
