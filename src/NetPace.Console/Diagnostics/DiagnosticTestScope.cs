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
/// <c>test.end</c> counts are derived from the records it saw, so the summary cannot count a
/// record that is not above it: its <c>bytes</c> total is every record's bytes added up, whatever
/// their outcome, which is what the measurement itself counts. It can also report a cancelled
/// count, which the measured result does not carry. Under-load latency probes are the one thing it
/// records without counting; see <see cref="Report"/> for why.
/// </remarks>
internal sealed class DiagnosticTestScope : IProgress<RequestDiagnostic>, IDisposable
{
    /// <summary>
    /// What a request stopped because the run ran out of time is recorded as.
    /// </summary>
    /// <remarks>
    /// <c>NetPace.Core</c> records such a request with its own generic wording
    /// (<see cref="RequestDiagnostic.StoppedByCaller"/>), because the library knows only that its
    /// caller asked it to stop. Here that caller is known, and the one thing that stops a NetPace
    /// run while keeping its measurement is the run's time limit - so the record names it, and a
    /// triager reading a cut-short run is told why it was cut short.
    /// </remarks>
    private const string TimeLimitReached = "run time limit reached";

    private readonly IDiagnosticRecorder recorder;
    private readonly string test;
    private readonly object countLock = new();

    private int requests;
    private int succeeded;
    private int failed;
    private int cancelled;
    private long bytes;
    private bool summaryWritten;

    /// <summary>
    /// Opens the scope, recording that the named test started. Private so <see cref="For"/> is the
    /// only way in, which is what makes "a scope exists" mean "something is being recorded".
    /// </summary>
    private DiagnosticTestScope(IDiagnosticRecorder recorder, string test)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentException.ThrowIfNullOrWhiteSpace(test);

        this.recorder = recorder;
        this.test = test;

        recorder.Record("test.start", ("test", test));
    }

    /// <summary>
    /// Opens a scope for the named test, or returns <see langword="null"/> when nothing is being
    /// recorded.
    /// </summary>
    /// <remarks>
    /// Returning null rather than a scope over a disabled recorder is what lets the provider skip
    /// the work: <c>NetPace.Core</c> tests the reporter for null before building a record, so a
    /// null scope means no <see cref="RequestDiagnostic"/>, no URL evaluation and - on a failure -
    /// no walk of the exception chain, per request. Paying that inside a measurement
    /// for a user who asked for no diagnostics is the perturbation this design avoids. A scope over
    /// a disabled recorder would also still take this type's lock and format every record before
    /// discarding it.
    /// </remarks>
    public static DiagnosticTestScope? For(IDiagnosticRecorder recorder, string test)
    {
        ArgumentNullException.ThrowIfNull(recorder);

        // Checked before the enabled test, so a blank name is a programmer error on every run
        // rather than one that only surfaces once --diagnostics is passed.
        ArgumentException.ThrowIfNullOrWhiteSpace(test);

        return recorder.IsEnabled ? new DiagnosticTestScope(recorder, test) : null;
    }

    /// <inheritdoc />
    public void Report(RequestDiagnostic value)
    {
        lock (countLock)
        {
            // A request abandoned at the screening ceiling can still finish after the pass has
            // moved on and this scope has written its summary. Counting it would be too late to
            // reach that summary, and recording it would put a request line below the test.end
            // that was supposed to total it - so the scope stops accepting reports once it has
            // written its summary, and that summary stays a true account of the records above it.
            if (summaryWritten)
            {
                return;
            }

            // A latency probe sent during a download or an upload is something the test does
            // alongside its own requests, not one of them: it gets a record of its own under
            // event=probe, carrying a request line's fields so it reads the same way, and it is
            // left out of the summary below - which is what keeps that summary reconciling with the
            // transfer's request records. In the screening and latency tests the probes are the
            // test, so there they stay ordinary request lines counted as today.
            var isLoadedLatencyProbe = value.Kind == RequestKind.LatencyProbe && DiagnosticTests.MeasuresATransfer(test);

            // Recorded before it is counted, and under the same lock, so the summary can never
            // total a record that is not above it: a throw here leaves the counters untouched, and
            // holding the lock stops a report passing the closed check and then landing its line
            // after Dispose has written the summary.
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
            // way: a request the server cut off, and one dropped at the byte budget, each moved
            // what their record says they moved. Summing only the successful ones would put a
            // figure in the summary that no reported speed agrees with.
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

    /// <summary>
    /// Closes the scope, recording the test's summary. Recording it is what closes the scope to
    /// further reports, so the summary totals exactly the records written above it.
    /// </summary>
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

    /// <summary>
    /// Why a request did not succeed, as the record words it: the provider's own reason, unless it
    /// is the generic stop the library records, which only the run's time limit asks for here.
    /// </summary>
    private static string? ReasonOf(RequestDiagnostic value) =>
        string.Equals(value.FailureReason, RequestDiagnostic.StoppedByCaller, StringComparison.Ordinal)
            ? TimeLimitReached
            : value.FailureReason;

    /// <summary>
    /// The wire value for an outcome. <c>ok</c> rather than <c>succeeded</c>, because
    /// <c>grep status=failed</c> is the whole triage workflow and the two must not share a prefix.
    /// </summary>
    private static string StatusOf(RequestOutcome outcome) => outcome switch
    {
        RequestOutcome.Succeeded => "ok",
        RequestOutcome.Failed => "failed",
        _ => "cancelled"
    };
}
