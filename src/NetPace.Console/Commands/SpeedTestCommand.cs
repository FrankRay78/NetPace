using NetPace.Core;
using NetPace.Console.ConsoleWriters;
using Spectre.Console.Extensions;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IWaiter waiter, CancellationToken cancellationToken) : CancelableCommand<SpeedTestCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.OutputFile))
        {
            // Wrap console with TeeAnsiConsole if file output is requested.
            console = new TeeAnsiConsole(console, settings.OutputFile, settings.FileModeValue);
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
                        await writer.PerformSpeedTestAsync(initialSpeedTest: firstLoop, console, clock, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
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
                        await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), console, clock, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
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
                    await writer.PerformSpeedTestAsync(initialSpeedTest: true, console, clock, speedTestClient, settings, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e)
                {
                    console.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
                }
            }

            return 0;
        }
        finally
        {
            if (console is TeeAnsiConsole teeConsole)
            {
                // Dispose of TeeAnsiConsole to flush and close file.
                teeConsole.Dispose();
            }
        }
    }
}