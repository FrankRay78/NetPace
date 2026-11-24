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
    /// Measured against the server the run settled on, never the screening figure the choice was
    /// made from. Absent under <c>--no-latency</c> rather than reported as a zero.
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
    /// Built here rather than inline, the outcome being the only input to <c>--fail-on</c> and the
    /// targets: a writer omitting a measurement would silently disable both for its own format.
    /// <para>
    /// The object initialiser is still reachable, so the pairing is held by the tests over each
    /// output format rather than by the type.
    /// </para>
    /// </remarks>
    /// <param name="latency">The idle latency measurement, or <see langword="null"/> if the latency test was not requested.</param>
    /// <param name="download">The download measurement, or <see langword="null"/> if the download test was not requested.</param>
    /// <param name="upload">The upload measurement, or <see langword="null"/> if the upload test was not requested.</param>
    public static SpeedTestOutcome Create(LatencyTestResult? latency, SpeedTestResult? download, SpeedTestResult? upload) =>
        new() { Latency = latency, Download = download, Upload = upload };
}
