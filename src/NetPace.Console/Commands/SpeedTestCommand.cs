using NetPace.Core;
using NetPace.Console.ConsoleWriters;
using Spectre.Console.Extensions;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IWaiter waiter, CancellationToken cancellationToken) : CancelableCommand<SpeedTestCommandSettings>(cancellationToken)
{
    protected override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Wrap console with TeeAnsiConsole if file output is requested
        TeeAnsiConsole? teeConsole = null;
        var effectiveConsole = console;

        if (!string.IsNullOrWhiteSpace(settings.OutputFile))
        {
            try
            {
                teeConsole = new TeeAnsiConsole(console, settings.OutputFile);
                effectiveConsole = teeConsole;
            }
            catch (Exception e)
            {
                console.Markup($"[red]Error creating output file:[/] {e.Message.EscapeMarkup()}\n");
                return 1;
            }
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
                        await writer.PerformSpeedTestAsync(initialSpeedTest: firstLoop, effectiveConsole, clock, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        effectiveConsole.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
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
                        await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), effectiveConsole, clock, speedTestClient, settings, cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e)
                    {
                        effectiveConsole.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
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
                    await writer.PerformSpeedTestAsync(initialSpeedTest: true, effectiveConsole, clock, speedTestClient, settings, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e)
                {
                    effectiveConsole.Markup($"[red]Error:[/] {e.Message.EscapeMarkup()}\n");
                }
            }

            return 0;
        }
        finally
        {
            // Dispose of TeeAnsiConsole to flush and close file
            teeConsole?.Dispose();
        }
    }
}