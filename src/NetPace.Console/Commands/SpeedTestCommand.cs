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
    /// counts and leave the exit code at <c>0</c> unless the consumer opts in via <c>--fail-on</c>.
    /// Only operational failures (which propagate out of this method to the top-level handler)
    /// produce a non-zero exit code.
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
                // Run continuously.
                var firstLoop = true;
                do
                {
                    try
                    {
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: firstLoop, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                        if (FailOnTriggered(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e) when (IsOperationalFault(e))
                    {
                        // NetPace's own health, not a network condition: exit non-zero.
                        throw;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);

                        // This measurement never completed, which --fail-on treats as a failure.
                        if (FailOnRequested(settings)) return 1;
                    }

                    firstLoop = false;

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
                // Run multiple times.
                for (int i = 0; i < settings.Count; i++)
                {
                    try
                    {
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                        if (FailOnTriggered(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e) when (IsOperationalFault(e))
                    {
                        // NetPace's own health, not a network condition: exit non-zero.
                        throw;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);

                        // This measurement never completed, which --fail-on treats as a failure.
                        if (FailOnRequested(settings)) return 1;
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
                    if (FailOnTriggered(outcome, settings)) return 1;
                }
                catch (OperationCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e) when (IsOperationalFault(e))
                {
                    // NetPace's own health, not a network condition: exit non-zero.
                    throw;
                }
                catch (Exception e)
                {
                    WriteError(console, e.Message);

                    // This measurement never completed, which --fail-on treats as a failure.
                    if (FailOnRequested(settings)) return 1;
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
    /// Writes an error message to the console.
    /// </summary>
    private static void WriteError(IAnsiConsole console, string message)
    {
        console.Markup($"[red]Error:[/] {message.EscapeMarkup()}\n");
    }

    /// <summary>
    /// Whether an exception reflects NetPace's own health rather than a network condition.
    /// Network conditions are reported and leave the exit code at <c>0</c>; operational faults
    /// (for example, the <c>--file</c> target becoming unwritable mid-run) must exit non-zero.
    /// <see cref="HttpIOException"/> is excluded because it derives from <see cref="IOException"/>:
    /// a reset connection is a network condition, not ours.
    /// </summary>
    private static bool IsOperationalFault(Exception e) =>
        e is (IOException and not HttpIOException) or UnauthorizedAccessException;

    /// <summary>
    /// Evaluates the <c>--fail-on</c> threshold against a single measurement (fail-fast).
    /// </summary>
    private static bool FailOnTriggered(SpeedTestOutcome outcome, SpeedTestCommandSettings settings)
    {
        if (settings.FailOn == FailOn.None) return false;

        foreach (var test in new[] { outcome.Download, outcome.Upload }.OfType<SpeedTestResult>())
        {
            if (settings.FailOn == FailOn.Total && test.IsAllFailed()) return true;
            if (settings.FailOn == FailOn.Partial && test.HasFailures()) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the consumer opted in to failure exit codes at all.
    /// </summary>
    /// <remarks>
    /// Used where a measurement threw rather than completing. <c>Partial</c> is the stricter
    /// threshold, so it must fire wherever <c>Total</c> does - a run that produced nothing cannot
    /// be more acceptable to a pristine-run check than one that produced an all-failed result.
    /// </remarks>
    private static bool FailOnRequested(SpeedTestCommandSettings settings) =>
        settings.FailOn != FailOn.None;
}
