using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// The outcome of a single speed-test run, returned by an <see cref="IConsoleWriter"/> so the
/// command can apply exit-code policy without re-running the test.
/// </summary>
public sealed record SpeedTestOutcome
{
    /// <summary>
    /// Gets the download measurement, or <see langword="null"/> when the download test was not requested.
    /// </summary>
    public SpeedTestResult? Download { get; init; }

    /// <summary>
    /// Gets the upload measurement, or <see langword="null"/> when the upload test was not requested.
    /// </summary>
    public SpeedTestResult? Upload { get; init; }

    /// <summary>
    /// Creates the outcome for a completed run from the measurements each test produced.
    /// </summary>
    /// <remarks>
    /// Every <see cref="IConsoleWriter"/> builds its outcome here rather than inline. The outcome is
    /// the only input to <c>--fail-on</c>, so a writer that omitted a measurement would silently
    /// disable the exit-code policy for its own output format with no build error to catch it.
    /// </remarks>
    /// <param name="download">The download measurement, or <see langword="null"/> if the download test was not requested.</param>
    /// <param name="upload">The upload measurement, or <see langword="null"/> if the upload test was not requested.</param>
    /// <returns>The outcome carrying both measurements.</returns>
    public static SpeedTestOutcome Create(SpeedTestResult? download, SpeedTestResult? upload) =>
        new() { Download = download, Upload = upload };
}
