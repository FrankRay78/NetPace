using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// The outcome of a single speed-test run, returned by an <see cref="IConsoleWriter"/> so the
/// command can apply exit-code policy and emit human-facing notices without re-running the test.
/// </summary>
public sealed record SpeedTestOutcome
{
    /// <summary>
    /// Gets a value indicating whether a usable server was found. When <see langword="false"/>, no
    /// measurement ran and no data row was written to standard output.
    /// </summary>
    public required bool ServersFound { get; init; }

    /// <summary>
    /// Gets the URL of the server the test ran against, or <see langword="null"/> when no server was found.
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>
    /// Gets the download measurement, or <see langword="null"/> when the download dimension was not requested.
    /// </summary>
    public SpeedTestResult? Download { get; init; }

    /// <summary>
    /// Gets the upload measurement, or <see langword="null"/> when the upload dimension was not requested.
    /// </summary>
    public SpeedTestResult? Upload { get; init; }

    /// <summary>
    /// An outcome representing that no usable server was found.
    /// </summary>
    public static readonly SpeedTestOutcome NoServers = new() { ServersFound = false };
}
