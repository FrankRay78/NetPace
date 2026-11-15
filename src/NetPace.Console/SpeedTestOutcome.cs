using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// The outcome of a single speed-test run, returned by an <see cref="IConsoleWriter"/> so the
/// command can apply exit-code policy without re-running the test.
/// </summary>
public sealed record SpeedTestOutcome
{
    /// <summary>
    /// Gets the idle latency measurement, or <see langword="null"/> when the latency test was not
    /// requested.
    /// </summary>
    /// <remarks>
    /// The headline latency figure every output format reports, measured against the server the
    /// run settled on, and never the screening figure the choice was made from. Absent under
    /// <c>--no-latency</c> rather than reported as a zero, on the same terms as the two transfers.
    /// </remarks>
    public LatencyTestResult? Latency { get; init; }

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
    /// Build outcomes through this method rather than inline, so every <see cref="IConsoleWriter"/>
    /// passes all three measurements from one place. The outcome is the only input to
    /// <c>--fail-on</c> and to the <c>--min-download</c>, <c>--min-upload</c> and
    /// <c>--max-latency</c> targets, so a writer that omitted a measurement would silently disable
    /// both for its own output format. The object initialiser is still reachable and does not
    /// enforce that, so the pairing is held by the tests over each output format, not by the type.
    /// </remarks>
    /// <param name="latency">The idle latency measurement, or <see langword="null"/> if the latency test was not requested.</param>
    /// <param name="download">The download measurement, or <see langword="null"/> if the download test was not requested.</param>
    /// <param name="upload">The upload measurement, or <see langword="null"/> if the upload test was not requested.</param>
    public static SpeedTestOutcome Create(LatencyTestResult? latency, SpeedTestResult? download, SpeedTestResult? upload) =>
        new() { Latency = latency, Download = download, Upload = upload };
}
