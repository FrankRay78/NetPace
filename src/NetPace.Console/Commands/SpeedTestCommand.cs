using System.Globalization;
using System.Text;
using NetPace.Console.ConsoleWriters;
using NetPace.Console.Diagnostics;
using NetPace.Console.Theming;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommand(IAnsiConsole console, ErrorOutput errorOutput, NoticeOutput noticeOutput, ISpeedTestService speedTestClient, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, IWaiter waiter, IRunTimeLimit runTimeLimit, ILiveDisplayRefresh liveDisplayRefresh, Theme theme)
{
    // Written in place of the exception's own wording, which names the configured timeout that
    // elapsed - NetPace's plumbing - rather than saying the server never answered.
    private const string TimedOutMessage = "The speed test failed because the server did not respond in time.";

    // What --loop asks for: only a cancellation, --fail-on or an operational fault stops the run.
    private const int Unbounded = -1;

    private bool _diagnosticWriteReported;

    // Network and discovery outcomes are data, not errors: they leave the exit code at 0 unless the
    // consumer opted in with --fail-on or a target. Only an operational fault, which propagates
    // past this method to the top-level handler, exits non-zero on its own.
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
            // From a finally, because a run that threw an operational fault is exactly when its
            // diagnostics matter most. exitCode is 1 here when RunAsync threw, which is what the
            // top-level handler returns.
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
            // Spectre's wrapping puts real newlines inside JSON string values and CSV header
            // names, so the width limit comes off here
            // (docs/change-intent-records/2026-10-15-machine-readable-output-is-never-wrapped.md).
            // Default output joins them wherever it cannot redraw, because there it writes the
            // --simple lines, which carry the same contract whichever switch asked for them.
            // CompositeAnsiConsole delegates Profile to its primary, so the terminal leg shares
            // this profile object and the lift reaches it wherever the composite is built.
            console.Profile.Width = int.MaxValue;
        }

        // The one destination this run opens, and so the only one it disposes: the terminal console
        // was handed over by a host that outlives this command and writes an operational fault to
        // it after this method throws. Whether the target already holds content is read as the file
        // is opened, before this run writes anything, because a run that appends is itself about to
        // add content.
        using FileConsole? openedFile = string.IsNullOrWhiteSpace(settings.OutputFile)
            ? null
            : new FileConsole(settings.OutputFile, settings.FileModeValue, StartsEmpty(settings));

        if (settings.Quiet || openedFile is not null)
        {
            var consoles = new List<IAnsiConsole>();

            if (!settings.Quiet)
            {
                consoles.Add(console);
            }

            if (openedFile is not null)
            {
                consoles.Add(openedFile);
            }

            console = new CompositeAnsiConsole(console, consoles.ToArray());
        }

        // The three ways to ask for a run - once, --count times, or --loop - differ in nothing
        // below but this, so every rule a run obeys is written once. --count carries no validator,
        // so the floor is what keeps a user's -1 off the Unbounded sentinel.
        int tests = settings.Loop ? Unbounded : Math.Max(settings.Count, 1);

        // Carried out of the loop rather than returned from inside it, so the run ends where every
        // other run ends.
        var missedThreshold = false;

        for (var test = 0; tests == Unbounded || test < tests; test++)
        {
            // Before each test but the first, rather than after each but the last, so where the run
            // ends is stated only in the loop condition above.
            if (test > 0)
            {
                try
                {
                    await waiter.Delay(settings.Delay, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
            }

            // Per test rather than per invocation, and disposed before the next pause, so the wait
            // between tests is billed to neither of the tests it sits between. Reaching the limit
            // is not a failure: the writer reports what was measured and the run carries on, unless
            // no server had been chosen yet.
            using var limit = runTimeLimit.Start(settings.TimeLimit);

            try
            {
                var outcome = await writer.PerformSpeedTestAsync(console, clock, clientInfoProvider, recorder, speedTestClient, settings, cancellationToken, limit.Token);

                // Reported before --fail-on decides anything, so a run that trips both still tells
                // the user what the line fell short of. Records flushed first, so on a shared error
                // stream what was measured reads before what it fell short of.
                FlushDiagnostics(settings);
                missedThreshold |= ReportMisses(outcome, settings);

                if (FailOnTriggered(outcome, settings)) return 1;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Nothing is reported about the targets: a run stopped part-way measured nothing to
                // hold to them.
                return 0;
            }
            catch (Exception e) when (OperationalFault.Matches(e))
            {
                throw;
            }
            catch (Exception e)
            {
                // Flushed before the error is written, because the two share the error stream
                // unless --diagnostics-file took the records elsewhere, so their order on it is the
                // order they are read in: what was attempted, then what went wrong. From a finally,
                // because a flush must not be able to cost the user the message.
                try
                {
                    FlushDiagnostics(settings);
                }
                finally
                {
                    errorOutput.WriteLine(theme, Describe(e));
                }

                // No outcome survived, so every target is missed: a check that cannot see the line
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

    // Whether the --file target holds nothing this run would be adding to, which is what decides
    // whether a CSV header is written
    // (docs/change-intent-records/2026-10-29-csv-header-is-decided-per-destination.md).
    private static bool StartsEmpty(SpeedTestCommandSettings settings)
    {
        if (settings.FileModeValue == FileMode.Overwrite) return true;

        var file = new FileInfo(settings.OutputFile);
        if (!file.Exists) return true;

        // A symlink reports the length of the path it stores, not of the file that will be written.
        // Only safe once the path is known to exist, because it throws otherwise.
        if (file.ResolveLinkTarget(returnFinalTarget: true) is FileInfo target)
        {
            file = target;
        }

        // Answered without opening the file: a named pipe or a terminal reports no length and would
        // block a read, and a log may be writable but not readable.
        if (!file.Exists || file.Length == 0) return true;

        // A byte-order mark and/or a bare line ending counts as empty, because a reader shows such
        // a file as empty and reading it as content would suppress that log's header for good.
        // NetPace writes no mark of its own
        // (docs/change-intent-records/2026-11-02-file-output-carries-no-byte-order-mark.md), but
        // `echo > log.csv` or an older log may leave one.
        var preamble = Encoding.UTF8.GetPreamble();
        if (file.Length > preamble.Length + 2) return false;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullName);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Taken as content, so it is appended to and never replaced.
            return false;
        }

        ReadOnlySpan<byte> content = bytes;
        if (content.StartsWith(preamble)) content = content[preamble.Length..];

        return content.IndexOfAnyExcept((byte)'\r', (byte)'\n') < 0;
    }

    // A destination that cannot be written is never raised: diagnostics are a second stream
    // alongside the result, and raising here would turn a completed measurement into a non-zero
    // exit, or replace the exception the records were written to explain. The records stay
    // buffered, so a later flush still carries them.
    //
    // Reported as a notice, so a script scanning the error stream for errors does not read a
    // succeeded run as failed, and only under --diagnostics-file: under --diagnostics alone the
    // stream that would carry the message is the one that just failed. Once per run
    // (docs/change-intent-records/2026-11-17-a-writer-remembers-its-own-once-per-run-line.md).
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

    // A timeout and a cancellation arrive as the same exception type, so they are told apart by
    // what the cancellation carries
    // (docs/change-intent-records/2026-11-08-a-timeout-is-a-cancellation-carrying-a-timeout.md). A
    // cancellation the caller asked for never reaches here: the catch above claims it.
    private static string Describe(Exception e) =>
        e is OperationCanceledException && e.InnerException is TimeoutException
            ? TimedOutMessage
            : e.Message;

    // To the error stream rather than through the console, so the result another program reads is
    // the same text with the targets as without them, and --quiet still hears that the line fell
    // short.
    private bool ReportMisses(SpeedTestOutcome? outcome, SpeedTestCommandSettings settings)
    {
        IReadOnlyList<string> misses = ThresholdCheck.Misses(outcome, settings);

        foreach (string miss in misses)
        {
            noticeOutput.Writer.WriteLine(miss);
        }

        return misses.Count > 0;
    }

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

    // Read where a measurement threw rather than completing, so neither threshold can be evaluated
    // against a result: a run that produced nothing cannot be more acceptable than one that
    // produced an all-failed result, so either setting fails it.
    private static bool FailOnRequested(SpeedTestCommandSettings settings) =>
        settings.FailOn != FailOn.None;
}
