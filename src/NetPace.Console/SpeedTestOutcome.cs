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
    /// Creates an outcome from a completed run, applying the convention that a test which was not
    /// requested is reported as <see langword="null"/> rather than as a zero measurement.
    /// </summary>
    /// <remarks>
    /// Every <see cref="IConsoleWriter"/> builds its outcome here. <see cref="SpeedTestOutcome"/> is
    /// the only input to <c>--fail-on</c>, so a writer that built its own would silently disable
    /// <c>--fail-on</c> for that output format with no build error.
    /// </remarks>
    /// <param name="settings">The settings the run used, which decide whether each test was requested.</param>
    /// <param name="downloadResult">The download measurement, ignored when the download test was not requested.</param>
    /// <param name="uploadResult">The upload measurement, ignored when the upload test was not requested.</param>
    /// <returns>The outcome of the run.</returns>
    public static SpeedTestOutcome For(SpeedTestCommandSettings settings, SpeedTestResult downloadResult, SpeedTestResult uploadResult)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new SpeedTestOutcome
        {
            Download = settings.NoDownload ? null : downloadResult,
            Upload = settings.NoUpload ? null : uploadResult
        };
    }
}
