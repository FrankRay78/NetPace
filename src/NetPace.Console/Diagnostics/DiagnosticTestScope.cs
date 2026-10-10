using System.Globalization;
using NetPace.Core;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Brackets one test - latency screening, latency measurement, download or upload - in the
/// diagnostic log: a <c>test.start</c> record on construction, one <c>request</c> record per
/// request reported by the provider, and a <c>test.end</c> summary on disposal.
/// </summary>
/// <remarks>
/// The scope is the <see cref="IProgress{T}"/> the provider reports requests to, so the test's name
/// is attached here rather than being something <c>NetPace.Core</c> has to know. Its
/// <c>test.end</c> counts are derived from the records it saw, so the summary cannot count a
/// record that is not above it - its <c>bytes</c> total covers the successful ones, which is less
/// than every record's bytes added up where a request moved data and was then excluded. It can also
/// report a cancelled count, which the measured result does not carry.
/// </remarks>
internal sealed class DiagnosticTestScope : IProgress<RequestDiagnostic>, IDisposable
{
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

            // Recorded before it is counted, and under the same lock, so the summary can never
            // total a record that is not above it: a throw here leaves the counters untouched, and
            // holding the lock stops a report passing the closed check and then landing its line
            // after Dispose has written the summary.
            recorder.RecordAt(
                value.StartedAt,
                "request",
                ("test", test),
                ("seq", value.Sequence.ToString(CultureInfo.InvariantCulture)),
                ("url", value.Url),
                ("status", StatusOf(value.Outcome)),
                ("bytes", value.BytesProcessed.ToString(CultureInfo.InvariantCulture)),
                ("duration_ms", value.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)),
                ("reason", value.FailureReason),
                ("exception", value.FailureType));

            requests++;

            switch (value.Outcome)
            {
                case RequestOutcome.Succeeded:
                    succeeded++;
                    bytes += value.BytesProcessed;
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
