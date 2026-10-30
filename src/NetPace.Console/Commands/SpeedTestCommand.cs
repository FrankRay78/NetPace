using System.Globalization;
using System.Text;
using NetPace.Console.ConsoleWriters;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, IWaiter waiter)
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
        var exitCode = 1;

        try
        {
            exitCode = await RunAsync(settings, cancellationToken);
            return exitCode;
        }
        finally
        {
            // Written after the last per-iteration flush, and from a finally because a run that
            // threw an operational fault is exactly when its diagnostics matter most. exitCode is
            // 1 here when RunAsync threw, which matches what the top-level handler returns.
            recorder.Record("run.end", ("exit", exitCode.ToString(CultureInfo.InvariantCulture)));
            FlushDiagnostics();
        }
    }

    private async Task<int> RunAsync(SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        IConsoleWriter writer = settings switch
        {
            { CSV: true } => new CSVConsoleWriter(),
            { Json: true } or { JsonPretty: true } => new JsonConsoleWriter(),
            { Minimal: true } => new MinimalConsoleWriter(),
            _ => new DefaultConsoleWriter()
        };

        if (writer.MachineReadable)
        {
            // Spectre wraps what it writes to the console's width - 80 when stdout is a pipe or a
            // redirect, the window width in a narrow terminal - and those breaks are real newlines in
            // the output, landing inside JSON string values and CSV header names. A record another
            // program reads carries only the breaks its own format defines, so the limit comes off for
            // these writers while the default human-readable output goes on wrapping to fit the
            // screen. CompositeAnsiConsole delegates Profile to its primary, so the terminal leg
            // shares this profile object and the lift reaches it wherever the composite is built.
            // It covers the error lines this command writes once it is running; a parse or
            // validation error reported before the command exists is written at the unlifted width.
            // --file output has never wrapped: FileConsole reports no width limit of its own.
            console.Profile.Width = int.MaxValue;
        }

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
                // Add file output. Whether the target already holds content is read here, once per
                // run and before the file is opened: a run that appends is itself about to add
                // content, so asking any later would answer about this run's own output.
                consoles.Add(new FileConsole(settings.OutputFile, settings.FileModeValue, StartsEmpty(settings)));
            }

            // Composite console based on output targets.
            console = new CompositeAnsiConsole(console, consoles.ToArray());
        }

        try
        {
            if (settings.Loop)
            {
                // Run continuously.
                var firstLoop = true;
                do
                {
                    try
                    {
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: firstLoop, console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken);
                        if (FailOnTriggered(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e) when (IsOperationalFault(e))
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);

                        if (FailOnRequested(settings)) return 1;
                    }

                    // Flushed per iteration so a long run reports as it goes and the buffer does
                    // not grow without bound.
                    FlushDiagnostics();

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
                        var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: (i == 0), console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken);
                        if (FailOnTriggered(outcome, settings)) return 1;
                    }
                    catch (OperationCanceledException)
                    {
                        // User requested cancellation.
                        return 0;
                    }
                    catch (Exception e) when (IsOperationalFault(e))
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        WriteError(console, e.Message);

                        if (FailOnRequested(settings)) return 1;
                    }

                    // Flushed per iteration so a long run reports as it goes and the buffer does
                    // not grow without bound.
                    FlushDiagnostics();

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
                    var outcome = await writer.PerformSpeedTestAsync(initialSpeedTest: true, console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken);
                    if (FailOnTriggered(outcome, settings)) return 1;
                }
                catch (OperationCanceledException)
                {
                    // User requested cancellation.
                    return 0;
                }
                catch (Exception e) when (IsOperationalFault(e))
                {
                    throw;
                }
                catch (Exception e)
                {
                    WriteError(console, e.Message);

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
    /// Whether the <c>--file</c> target holds nothing this run would be adding to: a path that does
    /// not exist yet, one that exists and is empty, or any path in overwrite mode, where whatever
    /// was there is replaced.
    /// </summary>
    private static bool StartsEmpty(SpeedTestCommandSettings settings)
    {
        if (settings.FileModeValue == FileMode.Overwrite) return true;

        var file = new FileInfo(settings.OutputFile);
        if (!file.Exists) return true;

        // A symlink reports the length of the path it stores, not of the file that will be written,
        // so ask the file it resolves to. A link whose target has been rotated away is a new log.
        // Resolving is only safe once the path is known to exist, because it throws otherwise.
        if (file.ResolveLinkTarget(returnFinalTarget: true) is FileInfo target)
        {
            file = target;
        }

        if (!file.Exists) return true;

        // A file holding only a byte-order mark and/or a line ending counts as empty, not as
        // content. FileConsole opens the file with Encoding.UTF8, which writes a byte-order mark
        // before anything else, so a run that ended before writing a row leaves a file that every
        // reader shows as empty; `echo > log.csv` leaves a bare line ending. Reading either as
        // content would suppress the header on that log for good, which is the one outcome this
        // check exists to prevent. FileConsole replaces such a file rather than appending to it,
        // so the header lands on the first line; anything else, however short, is kept.
        var preamble = Encoding.UTF8.GetPreamble();
        if (file.Length > preamble.Length + 2) return false;

        ReadOnlySpan<byte> content = File.ReadAllBytes(file.FullName);
        if (content.StartsWith(preamble)) content = content[preamble.Length..];

        return content.IndexOfAnyExcept((byte)'\r', (byte)'\n') < 0;
    }

    /// <summary>
    /// Writes the buffered diagnostic records out. A stream that cannot be written is left
    /// unreported rather than raised.
    /// </summary>
    /// <remarks>
    /// Diagnostics are a second stream alongside the result, so one that cannot be written must not
    /// decide the run's fate: raising here would turn a completed measurement into a non-zero exit,
    /// and from the <c>finally</c> in <see cref="ExecuteAsync"/> it would replace the very exception
    /// the records were written to explain. Nothing is written about the failure either, because
    /// there is nowhere to write it: the stream that would carry the message is the one that just
    /// failed, and <see cref="RunAsync"/> has already torn the console down by the time the last
    /// flush runs. The unwritten records stay buffered, so a later flush still carries them.
    /// </remarks>
    private void FlushDiagnostics()
    {
        try
        {
            recorder.Flush();
        }
        catch (IOException)
        {
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
