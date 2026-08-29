using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// The outcome of a single speed-test run, returned by an <see cref="IConsoleWriter"/> so the
/// command can apply exit-code policy and emit human-facing notices without re-running the test.
/// </summary>
public sealed record SpeedTestOutcome
{
    /// <summary>
    /// Gets the URL of the server the test ran against.
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>
    /// Gets the download measurement, or <see langword="null"/> when the download test was not requested.
    /// </summary>
    public SpeedTestResult? Download { get; init; }

    /// <summary>
    /// Gets the upload measurement, or <see langword="null"/> when the upload test was not requested.
    /// </summary>
    public SpeedTestResult? Upload { get; init; }
}
