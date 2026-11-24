using System.Globalization;
using NetPace.Core;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Brackets one test - latency screening, latency measurement, download or upload - in the
/// diagnostic log: a <c>test.start</c> record on construction, one <c>request</c> record per
/// request reported by the provider, a <c>probe</c> record per latency probe a transfer sent while
/// it ran, and a <c>test.end</c> summary on disposal.
/// </summary>
/// <remarks>
/// The scope is the <see cref="IProgress{T}"/> the provider reports requests to, so the test's name
/// is attached here rather than being something <c>NetPace.Core</c> has to know. Its
/// <c>test.end</c> counts are derived from the records it saw, so the summary cannot count a record
/// that is not above it. Under-load latency probes are the one thing it records without counting;
/// see <see cref="Report"/>.
/// </remarks>
internal sealed class DiagnosticTestScope : IProgress<RequestDiagnostic>, IDisposable
{
    // NetPace.Core records such a request with its own generic wording
    // (RequestDiagnostic.StoppedByCaller), because the library knows only that its caller asked it
    // to stop. Here the caller is known, and the one thing that stops a run while keeping its
    // measurement is the time limit.
    private const string TimeLimitReached = "run time limit reached";

    private readonly IDiagnosticRecorder recorder;
    private readonly string test;
    private readonly CancellationToken timeLimitToken;
    private readonly object countLock = new();

    private int requests;
    private int succeeded;
    private int failed;
    private int cancelled;
    private long bytes;
    private bool summaryWritten;

    // Private so For is the only way in, which is what makes "a scope exists" mean "something is
    // being recorded".
    private DiagnosticTestScope(IDiagnosticRecorder recorder, string test, CancellationToken timeLimitToken)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(test);

        this.recorder = recorder;
        this.test = test;
        this.timeLimitToken = timeLimitToken;

        recorder.Record("test.start", ("test", test));
    }

    // Null rather than a scope over a disabled recorder, which is what lets the provider skip the
    // work: NetPace.Core tests the reporter for null before building a record, so a null scope
    // means no RequestDiagnostic, no URL evaluation and no walk of the exception chain, per
    // request, for a user who asked for no diagnostics.
    //
    // timeLimitToken is for a test that reaches the library through a token linked to the limit
    // rather than as a stop: a request it reports cancelled once the limit is reached is recorded
    // as stopped by the limit, because the library can only call it cancelled by its caller.
    public static DiagnosticTestScope? For(IDiagnosticRecorder recorder, string test, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        // Checked before the enabled test, so a blank name is a programmer error on every run
        // rather than one that only surfaces once --diagnostics is passed.
        ArgumentException.ThrowIfNullOrWhiteSpace(test);

        return recorder.IsEnabled ? new DiagnosticTestScope(recorder, test, timeLimitToken) : null;
    }

    /// <inheritdoc />
    public void Report(RequestDiagnostic value)
    {
        lock (countLock)
        {
            // A request abandoned at the screening ceiling can still finish after this scope has
            // written its summary, and recording it would put a request line below the test.end
            // that was supposed to total it.
            if (summaryWritten)
            {
                return;
            }

            // A probe sent during a transfer is something the test does alongside its own
            // requests, not one of them, so it is left out of the summary below - which is what
            // keeps that summary reconciling with the transfer's request records. In the screening
            // and latency tests the probes are the test, and stay ordinary request lines.
            var isLoadedLatencyProbe = value.Kind == RequestKind.LatencyProbe && DiagnosticTests.MeasuresATransfer(test);

            // Recorded before it is counted, and under the same lock, so a throw here leaves the
            // counters untouched and no report can pass the closed check above and then land its
            // line after Dispose has written the summary.
            recorder.RecordAt(
                value.StartedAt,
                isLoadedLatencyProbe ? "probe" : "request",
                ("test", test),
                ("seq", value.Sequence.ToString(CultureInfo.InvariantCulture)),
                ("url", value.Url),
                ("status", StatusOf(value.Outcome)),
                ("bytes", value.BytesProcessed.ToString(CultureInfo.InvariantCulture)),
                ("duration_ms", value.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("reason", ReasonOf(value)),
                ("exception", value.FailureType));

            if (isLoadedLatencyProbe)
            {
                return;
            }

            requests++;

            // Totalled whatever the outcome, because the measurement counts the bytes the same
            // way: summing only the successful ones would put a figure in the summary that no
            // reported speed agrees with.
            bytes += value.BytesProcessed;

            switch (value.Outcome)
            {
                case RequestOutcome.Succeeded:
                    succeeded++;
                    break;
                case RequestOutcome.Failed:
                    failed++;
                    break;
                default:
                    cancelled++;
                    break;
            }
        }
    }

    // Recording the summary is what closes the scope to further reports, so it totals exactly the
    // records written above it.
    public void Dispose()
    {
        lock (countLock)
        {
            if (summaryWritten)
            {
                return;
            }

            summaryWritten = true;

            recorder.Record(
                "test.end",
                ("test", test),
                ("requests", requests.ToString(CultureInfo.InvariantCulture)),
                ("succeeded", succeeded.ToString(CultureInfo.InvariantCulture)),
                ("failed", failed.ToString(CultureInfo.InvariantCulture)),
                ("cancelled", cancelled.ToString(CultureInfo.InvariantCulture)),
                ("bytes", bytes.ToString(CultureInfo.InvariantCulture)));
        }
    }

    // The provider's own reason, unless it is the generic stop the library records, which only the
    // run's time limit asks for here.
    private string? ReasonOf(RequestDiagnostic value) =>
        string.Equals(value.FailureReason, RequestDiagnostic.StoppedByCaller, StringComparison.Ordinal)
        || (value.Outcome == RequestOutcome.Cancelled && timeLimitToken.IsCancellationRequested)
            ? TimeLimitReached
            : value.FailureReason;

    // ok rather than succeeded, because `grep status=failed` is the whole triage workflow and the
    // two must not share a prefix.
    private static string StatusOf(RequestOutcome outcome) => outcome switch
    {
        RequestOutcome.Succeeded => "ok",
        RequestOutcome.Failed => "failed",
        _ => "cancelled"
    };
}
