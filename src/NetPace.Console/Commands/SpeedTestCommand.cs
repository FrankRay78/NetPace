using NetPace.Core;
using NetPace.Console.ConsoleWriters;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IClientInfoProvider clientInfoProvider, IWaiter waiter)
{
    /// <summary>
    /// Writes an error message to the console.
    /// </summary>
    private static void WriteError(IAnsiConsole console, string message)
    {
        console.Markup($"[red]Error:[/] {message.EscapeMarkup()}\n");
    }

    /// <summary>
    /// Executes the speed test command using the provided settings.
    /// </summary>
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
                        // Run the speed test.
                        await writer.PerformSpeedTestAsync(initialSpeedTest: firstLoop, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);
                    }
                    finally
                    {
                        firstLoop = false;
                    }

                    try
                    {
                        // Pause before the next speed test.
                        await waiter.Delay(settings.Delay, cancellationToken);
                    }
                    catch (TaskCanceledException)
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
                        // Run the speed test.
                        await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);
                    }

                    if ((i + 1) < settings.Count)
                    {
                        try
                        {
                            // Pause before the next speed test.
                            await waiter.Delay(settings.Delay, cancellationToken);
                        }
                        catch (TaskCanceledException)
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
                    // Run the speed test.
                    await writer.PerformSpeedTestAsync(initialSpeedTest: true, console, clock, clientInfoProvider, speedTestClient, settings, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e)
                {
                    WriteError(console, e.Message);
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
}
