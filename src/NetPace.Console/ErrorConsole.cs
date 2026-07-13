namespace NetPace.Console;

/// <summary>
/// Wraps the <see cref="IAnsiConsole"/> that targets standard error.
/// </summary>
/// <remarks>
/// Standard error is the human/operational channel, kept separate from the machine-readable
/// standard-output stream. It carries measurement-failure notices (in interactive output modes),
/// the "no servers found" notice, live per-request failure reasons at debug verbosity, and
/// operational-failure errors. Machine formats (JSON, CSV) self-describe measurement validity via
/// the request counts, so they are never duplicated on standard error.
/// </remarks>
public sealed class ErrorConsole
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorConsole"/> class.
    /// </summary>
    /// <param name="console">The console that writes to standard error.</param>
    public ErrorConsole(IAnsiConsole console) => Console = console;

    /// <summary>
    /// Gets the console that writes to standard error.
    /// </summary>
    public IAnsiConsole Console { get; }
}
