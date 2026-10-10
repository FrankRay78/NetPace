using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// The scope that brackets one test in the diagnostic log. Its summary must total exactly the
/// request records written above it - a summary that disagrees with the lines a reader can see
/// makes the whole log untrustworthy for triage, which is its only job.
/// </summary>
public sealed class DiagnosticTestScopeTests
{
    [Fact]
    public void For_WhenNothingIsBeingRecorded_ReturnsNoScope()
    {
        // Given the recorder in play when --diagnostics was not passed.
        // When a scope is asked for.
        var scope = DiagnosticTestScope.For(NullDiagnosticRecorder.Instance, "download");

        // Then there is none, so the provider is handed no reporter and skips building records
        // nobody will read.
        Assert.Null(scope);
    }

    [Fact]
    public void For_WithABlankTestName_ThrowsWhetherOrNotDiagnosticsAreOn()
    {
        // Given a blank test name - a programmer error either way.
        // When a scope is asked for, with recording off and then on.
        // Then it is rejected both times: a guard that only fires under --diagnostics turns a
        // mistake into a crash that only one code path ever sees.
        Assert.Throws<ArgumentException>(() => DiagnosticTestScope.For(NullDiagnosticRecorder.Instance, " "));
        Assert.Throws<ArgumentException>(() => DiagnosticTestScope.For(new RecordingRecorder(), " "));
    }

    [Fact]
    public void Dispose_CalledTwice_RecordsOneSummary()
    {
        // Given a scope over a recorder that remembers what it was told.
        var recorder = new RecordingRecorder();
        var scope = DiagnosticTestScope.For(recorder, "download")!;

        // When it is disposed twice, as a nested using and an explicit call both would.
        scope.Dispose();
        scope.Dispose();

        // Then one summary was written, not two - two would double every count a reader adds up.
        Assert.Equal(1, recorder.Events.Count(e => e == "test.end"));
    }

    [Fact]
    public void Report_AfterTheSummaryIsWritten_IsNotRecordedAndDoesNotMoveTheCounts()
    {
        // Given a scope that has already closed, as the screening scope has by the time a candidate
        // abandoned at the ceiling finishes.
        var recorder = new RecordingRecorder();
        var scope = DiagnosticTestScope.For(recorder, "screening")!;

        scope.Report(Diagnostic(1, RequestOutcome.Succeeded));
        scope.Dispose();

        var eventsAtClose = recorder.Events.Count;

        // When a late report arrives.
        scope.Report(Diagnostic(2, RequestOutcome.Cancelled));

        // Then nothing is written for it. Its line would otherwise sit below the test.end that was
        // supposed to total it, and the summary would be a false account of the records above it.
        Assert.Equal(eventsAtClose, recorder.Events.Count);
        Assert.Equal("requests=1 succeeded=1 failed=0 cancelled=0", recorder.Summary);
    }

    [Fact]
    public void Dispose_SummarisesExactlyTheRecordsItAccepted()
    {
        // Given one request of each outcome.
        var recorder = new RecordingRecorder();

        using (var scope = DiagnosticTestScope.For(recorder, "download")!)
        {
            scope.Report(Diagnostic(1, RequestOutcome.Succeeded, bytes: 500));
            scope.Report(Diagnostic(2, RequestOutcome.Failed));
            scope.Report(Diagnostic(3, RequestOutcome.Cancelled));
        }

        // Then the summary counts each one once, and bytes accrue for the successful one only -
        // the counts are derived from the records, so they cannot disagree with them.
        Assert.Equal("requests=3 succeeded=1 failed=1 cancelled=1", recorder.Summary);
        Assert.Equal(500, recorder.SummaryBytes);
    }

    private static RequestDiagnostic Diagnostic(int sequence, RequestOutcome outcome, long bytes = 0) =>
        new()
        {
            StartedAt = new DateTime(1980, 1, 1, 10, 5, 0),
            Sequence = sequence,
            Url = "http://example.com/",
            Outcome = outcome,
            BytesProcessed = bytes,
            ElapsedMilliseconds = 10
        };

    /// <summary>
    /// A recorder that keeps what it was handed, so a test can assert on the records rather than
    /// on formatted text.
    /// </summary>
    private sealed class RecordingRecorder : IDiagnosticRecorder
    {
        public List<string> Events { get; } = [];

        public string? Summary { get; private set; }

        public long SummaryBytes { get; private set; }

        public bool IsEnabled => true;

        public void Record(string eventName, params (string Key, string? Value)[] fields)
        {
            Events.Add(eventName);

            if (eventName != "test.end")
            {
                return;
            }

            string Field(string key) => fields.Single(f => f.Key == key).Value!;

            Summary = $"requests={Field("requests")} succeeded={Field("succeeded")} failed={Field("failed")} cancelled={Field("cancelled")}";
            SummaryBytes = long.Parse(Field("bytes"));
        }

        public void RecordAt(DateTime timestamp, string eventName, params (string Key, string? Value)[] fields) =>
            Record(eventName, fields);

        public void Flush()
        {
        }
    }
}
