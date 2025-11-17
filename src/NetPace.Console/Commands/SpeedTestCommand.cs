using NetPace.Core;
using NetPace.Console.ConsoleWriters;
using Spectre.Console.Extensions;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IWaiter waiter, CancellationToken cancellationToken) : CancelableCommand<SpeedTestCommandSettings>(cancellationToken)
{
    /// <summary>
    /// Writes an error message to the appropriate output stream.
    /// In quiet mode, errors go to stderr. Otherwise, they go through the console.
    /// </summary>
    private static void WriteError(IAnsiConsole console, string message)
    {
        // In quiet mode (NullAnsiConsole or FileOnlyConsole), write errors to stderr
        if (console is NullAnsiConsole or FileOnlyConsole)
        {
            System.Console.Error.WriteLine($"Error: {message}");
        }
        else
        {
            // Normal mode: write through Spectre.Console with formatting
            console.Markup($"[red]Error:[/] {message.EscapeMarkup()}\n");
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Handle quiet mode: suppress console output
        if (settings.Quiet)
        {
            if (!string.IsNullOrWhiteSpace(settings.OutputFile))
            {
                // Quiet mode with file output: write only to file, not console
                var fileWriter = new StreamWriter(settings.OutputFile, append: settings.FileModeValue == FileMode.Append, System.Text.Encoding.UTF8) { AutoFlush = true };
                console = new FileOnlyConsole(console, fileWriter);
            }
            else
            {
                // Quiet mode without file: suppress all output
                console = new NullAnsiConsole(console);
            }
        }
        else if (!string.IsNullOrWhiteSpace(settings.OutputFile))
        {
            // Normal mode with file output: write to both console and file
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
                        await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), console, clock, speedTestClient, settings, cancellationToken);
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
            // Dispose of console wrappers to flush and close file if applicable
            if (console is TeeAnsiConsole teeConsole)
            {
                teeConsole.Dispose();
            }
            else if (console is FileOnlyConsole fileOnlyConsole)
            {
                fileOnlyConsole.Dispose();
            }
        }
    }
}