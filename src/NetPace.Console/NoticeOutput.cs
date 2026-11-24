namespace NetPace.Console;

/// <summary>
/// Where a word to the user that is neither the result nor an error goes, such as a theme name
/// that is not a theme, a diagnostics file that could not be written or a target the run missed:
/// the process error stream in production, a buffer in tests.
/// </summary>
/// <remarks>
/// A notice is neither the result nor a diagnostic. It goes to the error stream, so a run whose
/// result another program is reading is never handed a sentence of English mid-data.
/// <para>
/// A plain <see cref="TextWriter"/> rather than an <see cref="IAnsiConsole"/>: the message is one
/// line whatever the width, and a <c>[</c> in it would be markup to Spectre.Console.
/// </para>
/// </remarks>
public sealed class NoticeOutput(TextWriter writer)
{
    /// <summary>
    /// Gets the writer notices are written to.
    /// </summary>
    public TextWriter Writer { get; } = writer;
}
