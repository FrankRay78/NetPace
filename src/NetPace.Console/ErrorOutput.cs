using NetPace.Console.Theming;

namespace NetPace.Console;

/// <summary>
/// Where every error NetPace reports goes: the process error stream in production, a buffer in
/// tests.
/// </summary>
/// <remarks>
/// An error is not a result. It goes to the error stream so standard output and a <c>--file</c>
/// target carry only the format a script or a log is reading, and so a run whose output is
/// redirected still shows the user what went wrong. It keeps its own console so a test can assert
/// which stream carried what.
/// <para>
/// An <see cref="IAnsiConsole"/> rather than the plain <see cref="TextWriter"/> that
/// <see cref="NoticeOutput"/> and <see cref="Diagnostics.DiagnosticOutput"/> use, because an error
/// is drawn in the theme: the word that marks it is coloured where the run has colour at all. The
/// width limit comes off, so the message is one unbroken line wherever the stream goes — a reader
/// greps an error log by line, and a console wrapping at terminal width would split one error
/// across several.
/// </para>
/// </remarks>
public sealed class ErrorOutput
{
    private readonly bool styled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorOutput"/> class.
    /// </summary>
    /// <param name="console">
    /// The console errors are written to. This type takes it over and removes its width limit, so
    /// it must be the error stream's alone: a console whose profile is shared with the result —
    /// which <see cref="CompositeAnsiConsole"/>'s is with its primary's — would lose its own limit
    /// too.
    /// </param>
    /// <param name="styled">
    /// Whether an error is drawn in the theme at all. <c>false</c> where the stream is not a
    /// terminal, so a captured error log holds the words and no escape bytes: taking colour away
    /// from a console still leaves emphasis such as bold, which is written as an escape sequence.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="console"/> is <c>null</c>.</exception>
    public ErrorOutput(IAnsiConsole console, bool styled = true)
    {
        ArgumentNullException.ThrowIfNull(console);

        console.Profile.Width = int.MaxValue;
        Console = console;
        this.styled = styled;
    }

    /// <summary>
    /// Gets the console errors are written to.
    /// </summary>
    public IAnsiConsole Console { get; }

    /// <summary>
    /// Writes one error, marked as such, on a line of its own.
    /// </summary>
    /// <param name="theme">The colours and emphasis to draw in, where errors are styled at all.</param>
    /// <param name="message">What went wrong, in the words the user should read.</param>
    public void WriteLine(Theme theme, string message) =>
        Console.WriteErrorLine(styled ? theme : Theme.Plain, message);
}
