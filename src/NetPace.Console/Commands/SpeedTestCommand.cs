using NetPace.Core;
using NetPace.Console.ConsoleWriters;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IClientInfoProvider clientInfoProvider, IWaiter waiter)
{
    /// <summary>
    /// Executes the speed test command using the provided settings.
    /// </summary>
    /// <remarks>
    /// Network and discovery outcomes are data, not errors: they are reported through the output
    /// (counts) and human-readable notices, and leave the exit code at <c>0</c> unless the consumer
    /// opts in via <c>--fail-on</c>. Only operational failures (which propagate out of this method to
    /// the top-level handler) produce a non-zero exit code.
    /// </remarks>
    public async Task<int> ExecuteAsync(SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Quiet || !string.IsNullOrWhiteSpace(settings.OutputFile))
        {
            var consoles = new List<IAnsiConsole>();

            if (!settings.Quiet)
            {
                // Add terminal output.
                consoles.Add(console);
            }

            if (!string.IsNullOrWhiteSpace(settings.OutputFile))
            {
                // Add file output.
                consoles.Add(new FileConsole(settings.OutputFile, settings.FileModeValue));
            }

            // Composite console based on output targets.
            console = new CompositeAnsiConsole(console, consoles.ToArray());
        }

        try
        {
            IConsoleWriter writer = settings switch
            {
                { CSV: true } => new CSVConsoleWriter(),
                { Json: true } or { JsonPretty: true } => new JsonConsoleWriter(),
                { Verbosity: Verbosity.Minimal } => new MinimalConsoleWriter(),
                _ => new DefaultConsoleWriter()
            };

            if (settings.Loop)
            {
                // Run continuously. `firstWrite` tracks whether a data row has actually been written
                // (not merely the iteration index), so a header-emitting writer (CSV) still prints
                // its header on the first successful row when earlier iterations found no server.
                var firstWrite = true;
                do
                {
                    try
                    {
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: firstWrite, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                        if (outcome.ServersFound) firstWrite = false;
                        if (ProcessOutcome(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }

                    try
                    {
                        // Pause before the next speed test.
                        await waiter.Delay(settings.Delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                }
                while (true);
            }
            else if (settings.Count > 1)
            {
                // Run multiple times. `firstWrite` tracks the first actual data row (see the loop
                // branch) so the CSV header survives leading iterations that found no server.
                var firstWrite = true;
                for (int i = 0; i < settings.Count; i++)
                {
                    try
                    {
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: firstWrite, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                        if (outcome.ServersFound) firstWrite = false;
                        if (ProcessOutcome(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }

                    if ((i + 1) < settings.Count)
                    {
                        try
                        {
                            // Pause before the next speed test.
                            await waiter.Delay(settings.Delay, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            // User requested cancellation.
                            return 0;
                        }
                    }
                }
            }
            else
            {
                // Run once.
                try
                {
                    var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: true, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                    if (ProcessOutcome(outcome, settings)) return 1;
                }
                catch (OperationCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
            }

            return 0;
        }
        finally
        {
            if (console is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Emits human-readable notices for the outcome and evaluates the <c>--fail-on</c> threshold.
    /// </summary>
    /// <returns><see langword="true"/> when <c>--fail-on</c> is met and the process should exit with a non-zero code.</returns>
    private bool ProcessOutcome(SpeedTestOutcome outcome, SpeedTestCommandSettings settings)
    {
        if (!outcome.ServersFound)
        {
            // No usable server is a reported data outcome, not an error (exit code stays 0).
            if (ShouldEmitFailureNotice(settings))
            {
                console.WriteLine("No speed test servers were found.");
            }

            return false;
        }

        // Machine formats (JSON, CSV) self-describe via the counts, and Minimal keeps the token
        // annotation only, so neither gets a duplicate notice.
        if (ShouldEmitFailureNotice(settings))
        {
            EmitAllFailedNotice("Download", settings.NoDownload ? null : outcome.Download, outcome.ServerUrl);
            EmitAllFailedNotice("Upload", settings.NoUpload ? null : outcome.Upload, outcome.ServerUrl);
        }

        return FailOnTriggered(outcome, settings);
    }

    /// <summary>
    /// Whether a failure notice should be written for the active output mode. Notices share the
    /// output stream with the results, so they are suppressed wherever that stream is
    /// machine-readable and would be corrupted by prose.
    /// </summary>
    private static bool ShouldEmitFailureNotice(SpeedTestCommandSettings settings) =>
        !settings.CSV && !settings.Json && !settings.JsonPretty && settings.Verbosity != Verbosity.Minimal;

    private void EmitAllFailedNotice(string dimension, SpeedTestResult? result, string? serverUrl)
    {
        if (result is not null && result.IsAllFailed())
        {
            console.WriteLine($"{dimension} failed: all {result.RequestsAttempted} requests to {serverUrl} failed.");
        }
    }

    /// <summary>
    /// Evaluates the <c>--fail-on</c> threshold against a single measurement (fail-fast).
    /// </summary>
    private static bool FailOnTriggered(SpeedTestOutcome outcome, SpeedTestCommandSettings settings)
    {
        if (settings.FailOn == FailOn.None) return false;

        foreach (var dimension in new[] { outcome.Download, outcome.Upload })
        {
            if (dimension is null) continue;

            if (settings.FailOn == FailOn.Total && dimension.IsAllFailed()) return true;
            if (settings.FailOn == FailOn.Partial && dimension.HasFailures()) return true;
        }

        return false;
    }
}
