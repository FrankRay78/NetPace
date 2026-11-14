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
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorOutput"/> class.
    /// </summary>
    /// <param name="console">
    /// The console errors are written to. This type takes it over and removes its width limit, so
    /// it must be the error stream's alone: a console whose profile is shared with the result —
    /// which <see cref="CompositeAnsiConsole"/>'s is with its primary's — would lose its own limit
    /// too.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="console"/> is <c>null</c>.</exception>
    public ErrorOutput(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        console.Profile.Width = int.MaxValue;
        Console = console;
    }

    /// <summary>
    /// Gets the console errors are written to.
    /// </summary>
    public IAnsiConsole Console { get; }
}
