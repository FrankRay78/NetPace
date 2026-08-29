using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Interface for writing speed test results to the console.
/// </summary>
public interface IConsoleWriter
{
    /// <summary>
    /// Whether this writer's output can carry a free-text notice alongside the results.
    /// </summary>
    /// <remarks>
    /// A notice shares the output stream with the results, so it is only safe where an extra line
    /// of prose cannot break what the writer promised its consumer: CSV promises delimited records,
    /// JSON promises parseable documents, and Minimal promises exactly one line per run.
    /// </remarks>
    bool AcceptsProseNotices { get; }

    /// <summary>
    /// Performs a speed test and writes the result to the console.
    /// </summary>
    /// <param name="initialSpeedTest">
    /// <see langword="true"/> if this is the first test in a sequence; used by writers that emit a
    /// header row (e.g. CSV) to decide whether to include it.
    /// </param>
    /// <param name="console">The Spectre.Console instance used for output.</param>
    /// <param name="clock">Clock used to obtain the current timestamp for each result.</param>
    /// <param name="clientInfoProvider">Provider for device identity values (IP address and hostname).</param>
    /// <param name="speedTestClient">Speed test service that performs latency, download and upload measurements.</param>
    /// <param name="settings">Parsed command-line settings controlling which measurements to run and how to format output.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the operation.</param>
    /// <returns>
    /// The outcome of the run (whether a server was found and the per-test measurements),
    /// used by the command to apply exit-code policy and emit failure notices.
    /// </returns>
    Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken);
}
