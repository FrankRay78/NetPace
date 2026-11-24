using System.Globalization;
using System.Text;
using NetPace.Console.ConsoleWriters;
using NetPace.Console.Diagnostics;
using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ErrorOutput errorOutput, NoticeOutput noticeOutput, ISpeedTestService speedTestClient, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, IWaiter waiter, IRunTimeLimit runTimeLimit, ILiveDisplayRefresh liveDisplayRefresh, Theme theme)
{
    /// <summary>
    /// What the user is told about a request abandoned at its own timeout: one composed line for
    /// every timeout that surfaces as an exception - a latency probe or the server-list fetch. The
    /// exception's own wording names the configured timeout that elapsed, which describes NetPace's
    /// own plumbing rather than telling the user the server never answered.
    /// </summary>
    private const string TimedOutMessage = "The speed test failed because the server did not respond in time.";

    /// <summary>
    /// The test count that stands for a run with no end of its own: what <c>--loop</c> asks for,
    /// where only a cancellation, <c>--fail-on</c> or an operational fault stops the run.
    /// </summary>
    private const int Unbounded = -1;

    /// <summary>
    /// Whether the user has already been told that the diagnostics file could not be written, so a
    /// run that keeps failing to write says it once.
    /// </summary>
    private bool _diagnosticWriteReported;

    /// <summary>
    /// Executes the speed test command using the provided settings.
    /// </summary>
    /// <remarks>
    /// A network outcome is data, not an error, and leaves the exit code at <c>0</c> unless the
    /// consumer opts in. Only an operational fault, which propagates, exits non-zero of itself.
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
            // From a finally, because a run that threw an operational fault is when its
            // diagnostics matter most. exitCode is 1 here, matching the top-level handler.
            recorder.Record("run.end", ("exit", exitCode.ToString(CultureInfo.InvariantCulture)));
            FlushDiagnostics(settings);
        }
    }

    private async Task<int> RunAsync(SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        IConsoleWriter writer = settings switch
        {
            { CSV: true } => new CSVConsoleWriter(),
            { Json: true } or { JsonPretty: true } => new JsonConsoleWriter(),
            { Minimal: true } => new MinimalConsoleWriter(),
            { Simple: true } => new SimpleConsoleWriter(),
            _ => new DefaultConsoleWriter(liveDisplayRefresh, theme)
        };

        if (writer.MachineReadable || !DefaultConsoleWriter.CanAnimate(console))
        {
            // Spectre's wrapping puts real newlines inside JSON values and CSV header names, so
            // the width limit comes off for a record another program reads.
            console.Profile.Width = int.MaxValue;
        }

        // The only destination this run closes: the host's terminal console outlives the command
        // and still carries an operational fault after it throws. Null where no file is written.
        using FileConsole? openedFile = string.IsNullOrWhiteSpace(settings.OutputFile)
            ? null
            : new FileConsole(settings.OutputFile, settings.FileModeValue, StartsEmpty(settings));

        if (settings.Quiet || openedFile is not null)
        {
            var consoles = new List<IAnsiConsole>();

            if (!settings.Quiet)
            {
                // Add terminal output.
                consoles.Add(console);
            }

            if (openedFile is not null)
            {
                // Add file output.
                consoles.Add(openedFile);
            }

            // Composite console based on output targets.
            console = new CompositeAnsiConsole(console, consoles.ToArray());
        }

        // One count for all three ways to ask for a run, so every rule below is written once.
        // Floored at one, which also keeps a user's -1 off the Unbounded sentinel.
        int tests = settings.Loop ? Unbounded : Math.Max(settings.Count, 1);

        // Carried out of the loop rather than returned from inside it, so a run ends where every
        // other run ends.
        var missedThreshold = false;

        for (var test = 0; tests == Unbounded || test < tests; test++)
        {
            // Before each test but the first, not after each but the last, so where the run ends
            // is stated only in the loop condition above.
            if (test > 0)
            {
                try
                {
                    await waiter.Delay(settings.Delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // User requested cancellation.
                    return 0;
                }
            }

            // Per test, not per invocation, and disposed before the next pause, so the wait
            // between tests is billed to neither of them.
            using var limit = runTimeLimit.Start(settings.TimeLimit);

            try
            {
                var outcome = await writer.PerformSpeedTestAsync(console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken, limit.Token);

                // Reported before --fail-on decides anything, so a run tripping both still says
                // what the line fell short of even though --fail-on takes the exit code.
                FlushDiagnostics(settings);
                missedThreshold |= ReportMisses(outcome, settings);

                if (FailOnTriggered(outcome, settings)) return 1;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User requested cancellation. Nothing is reported about the targets: a run
                // stopped part-way measured nothing to hold to them.
                return 0;
            }
            catch (Exception e) when (OperationalFault.Matches(e))
            {
                throw;
            }
            catch (Exception e)
            {
                // Flushed before the error, so a shared error stream reads in the order that
                // explains the failure. From a finally, so a flush cannot cost the user the message.
                try
                {
                    FlushDiagnostics(settings);
                }
                finally
                {
                    errorOutput.WriteLine(theme, Describe(e));
                }

                // No figure survived, so every target is missed: a check that cannot see the line
                // is not a check that passed.
                missedThreshold |= ReportMisses(outcome: null, settings);

                if (FailOnRequested(settings)) return 1;
            }

            // Flushed per iteration so a long run reports as it goes and the buffer does
            // not grow without bound.
            FlushDiagnostics(settings);
        }

        return missedThreshold ? ThresholdCheck.MissedExitCode : 0;
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

        // A symlink reports the length of the path it stores, so ask what it resolves to; safe
        // only once the path is known to exist, because resolving throws otherwise.
        if (file.ResolveLinkTarget(returnFinalTarget: true) is FileInfo target)
        {
            file = target;
        }

        // Answered without opening the file: a named pipe or terminal would block a read, and a
        // log may be writable but not readable.
        if (!file.Exists || file.Length == 0) return true;

        // A byte-order mark and/or a bare line ending counts as empty: a reader shows such a file
        // as empty, and reading it as content would suppress that log's header for good.
        var preamble = Encoding.UTF8.GetPreamble();
        if (file.Length > preamble.Length + 2) return false;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be read is taken as content, so it is appended to and never
            // replaced.
            return false;
        }

        ReadOnlySpan<byte> content = bytes;
        if (content.StartsWith(preamble)) content = content[preamble.Length..];

        return content.IndexOfAnyExcept((byte)'\r', (byte)'\n') < 0;
    }

    /// <summary>
    /// Writes the buffered diagnostic records out. A destination that cannot be written is never
    /// raised, and is reported to the user only where there is somewhere to report it.
    /// </summary>
    /// <remarks>
    /// A second stream must not decide the run's fate, and from the <c>finally</c> in
    /// <see cref="ExecuteAsync"/> a raise here would replace the very exception the records explain.
    /// <para>
    /// Where <c>--diagnostics-file</c> named the destination there is somewhere else to report it:
    /// once per run, as a notice rather than an error, since the run succeeded.
    /// </para>
    /// </remarks>
    /// <param name="settings">The settings the run was asked for, which name the destination.</param>
    private void FlushDiagnostics(SpeedTestCommandSettings settings)
    {
        try
        {
            recorder.Flush();
        }
        catch (IOException e)
        {
            if (!_diagnosticWriteReported && !string.IsNullOrWhiteSpace(settings.DiagnosticsFile))
            {
                _diagnosticWriteReported = true;
                noticeOutput.Writer.WriteLine($"The diagnostics could not be written to {settings.DiagnosticsFile}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// What to tell the user about a network condition that ended a test: the exception's own
    /// message, unless the request was abandoned at its own timeout rather than failing on its own
    /// merits.
    /// </summary>
    /// <remarks>
    /// A timeout and a cancellation arrive as the same type, so they are told apart by what the
    /// cancellation carries: a <see cref="TimeoutException"/> inside means a timeout.
    /// </remarks>
    private static string Describe(Exception e) =>
        e is OperationCanceledException && e.InnerException is TimeoutException
            ? TimedOutMessage
            : e.Message;

    /// <summary>
    /// Writes one line per target the run missed, and says whether there were any.
    /// </summary>
    /// <remarks>
    /// To the error stream, not the console: the result another program reads must be the same
    /// text with the targets as without, and <c>--quiet</c> still wants to hear about a miss.
    /// </remarks>
    private bool ReportMisses(SpeedTestOutcome? outcome, SpeedTestCommandSettings settings)
    {
        IReadOnlyList<string> misses = ThresholdCheck.Misses(outcome, settings);

        foreach (string miss in misses)
        {
            noticeOutput.Writer.WriteLine(miss);
        }

        return misses.Count > 0;
    }

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
    /// <c>Partial</c> is the stricter threshold, so it must fire wherever <c>Total</c> does: a run
    /// that produced nothing cannot be more acceptable than one that produced an all-failed result.
    /// </remarks>
    private static bool FailOnRequested(SpeedTestCommandSettings settings) =>
        settings.FailOn != FailOn.None;
}
