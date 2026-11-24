using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Interface for writing speed test results to the console.
/// </summary>
public interface IConsoleWriter
{
    /// <summary>
    /// Whether this writer's output is a record another program reads, rather than prose for a
    /// person. Machine-readable output must reach its destination exactly as the format composed
    /// it, so the run lifts the console's width limit for these writers and leaves human-readable
    /// output free to wrap to the screen.
    /// </summary>
    bool MachineReadable { get; }

    /// <summary>
    /// Performs a speed test and writes the result to the console.
    /// </summary>
    /// <remarks>
    /// One writer serves a whole run, so a format with a once-per-run line may remember it for
    /// itself. That line belongs above the first test that writes a result, not the first attempted.
    /// </remarks>
    /// <param name="console">The Spectre.Console instance used for output.</param>
    /// <param name="clock">Clock used to obtain the current timestamp for each result.</param>
    /// <param name="clientInfoProvider">Provider for device identity values (IP address and hostname).</param>
    /// <param name="recorder">Collects the run's diagnostic records; the null recorder when <c>--diagnostics</c> was not passed.</param>
    /// <param name="speedTestClient">Speed test service that performs latency, download and upload measurements.</param>
    /// <param name="settings">Parsed command-line settings controlling which measurements to run and how to format output.</param>
    /// <param name="cancellationToken">Token that can be used to cancel the operation.</param>
    /// <param name="timeLimitToken">
    /// Fires when the run reaches its time limit. A measurement already running is asked to stop
    /// and report what it has; a test whose turn has not come is not run, and is reported as a
    /// test nobody asked for is. Nothing is written about the limit: a run that measured for as
    /// long as it was allowed has not failed.
    /// </param>
    /// <returns>
    /// The download and upload measurements this run produced, each <see langword="null"/> when
    /// that test was not requested; used by the command to apply exit-code policy.
    /// </returns>
    Task<SpeedTestOutcome> PerformSpeedTestAsync(IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken, CancellationToken timeLimitToken = default);
}
