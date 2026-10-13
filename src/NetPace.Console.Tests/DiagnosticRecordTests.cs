using NetPace.Console.Diagnostics;

namespace NetPace.Console.Tests;

/// <summary>
/// The diagnostic record format: one logfmt line per record, self-describing, and never broken
/// across lines whatever a value contains.
/// </summary>
public sealed class DiagnosticRecordTests
{
    private static readonly DateTime Timestamp = new DateTime(1980, 1, 1, 10, 5, 0);

    [Fact]
    public void Format_WithPlainValues_LeadsWithTheTimestampThenTheEventName()
    {
        // Given plain values needing no quoting.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(Timestamp, "test.end", ("test", "download"), ("requests", "8"));

        // Then every field appears in the order given, behind the timestamp and the event name.
        Assert.Equal("ts=1980-01-01T10:05:00.000 event=test.end test=download requests=8", record);
    }

    [Fact]
    public void Format_WithAnAbsentValue_OmitsTheFieldEntirely()
    {
        // Given a field whose value is absent - a latency figure under --no-latency, say.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(Timestamp, "server.selected", ("selection", "first-in-list"), ("latency_ms", null));

        // Then the key does not appear at all, rather than appearing empty.
        Assert.Equal("ts=1980-01-01T10:05:00.000 event=server.selected selection=first-in-list", record);
    }

    [Theory]
    [InlineData("Foo Telecom", "\"Foo Telecom\"")]
    [InlineData("a=b", "\"a=b\"")]
    [InlineData("say \"hi\"", "\"say \\\"hi\\\"\"")]
    [InlineData("a\tb", "\"a\\tb\"")]
    [InlineData("plain", "plain")]
    public void Format_QuotesAValueOnlyWhenItNeedsIt(string value, string expected)
    {
        // Given a value that may or may not need quoting.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(Timestamp, "server.selected", ("sponsor", value));

        // Then it is bare unless it carries a space, an equals sign, a quote or a tab - whitespace
        // a reader splitting fields would otherwise take for a separator.
        Assert.Equal($"ts=1980-01-01T10:05:00.000 event=server.selected sponsor={expected}", record);
    }

    [Fact]
    public void Format_WithABackslashTerminatedValue_DoesNotSwallowTheFieldsAfterIt()
    {
        // Given a value that needs quoting and ends in a backslash - a Windows path, or an IO
        // exception message quoting one.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(
            Timestamp,
            "run.invocation",
            ("args", @"--file C:\my results\"),
            ("mode", "csv"));

        // Then the backslash is escaped, so the closing quote still closes the value and the field
        // after it is still a field rather than part of it.
        Assert.Equal(
            @"ts=1980-01-01T10:05:00.000 event=run.invocation args=""--file C:\\my results\\"" mode=csv",
            record);
    }

    [Fact]
    public void Format_WithAnEmptyValue_EmitsAnEmptyQuotedField()
    {
        // Given a field present but carrying nothing.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(Timestamp, "server.selected", ("location", string.Empty));

        // Then it is quoted, so the key does not run into whatever follows it.
        Assert.Equal("ts=1980-01-01T10:05:00.000 event=server.selected location=\"\"", record);
    }

    [Fact]
    public void Record_WritesNothingUntilFlushed()
    {
        // Given a recorder over a writer nobody has flushed.
        using var writer = new StringWriter();
        var recorder = new BufferingDiagnosticRecorder(new IncrementingDiagnosticClockStub(), writer);

        // When records are made but not flushed.
        recorder.Record("test.start", ("test", "download"));
        recorder.RecordAt(Timestamp, "request", ("seq", "1"));

        // Then nothing has reached the writer. Buffering is what keeps per-request detail from
        // interleaving with the live progress display and from perturbing the measurement it is
        // describing; writing as it happens would do both.
        Assert.Equal(string.Empty, writer.ToString());

        // And flushing releases exactly what was buffered.
        recorder.Flush();
        Assert.Equal(2, writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Format_WithAMultiLineValue_StaysOnOneLine()
    {
        // Given an exception message spanning lines, as they frequently do.
        // When the record is formatted.
        var record = DiagnosticRecord.Format(Timestamp, "request", ("reason", "first line\r\nsecond line"));

        // Then the record is still one line: a grepped subset must not lose half its records.
        Assert.DoesNotContain('\n', record);
        Assert.DoesNotContain('\r', record);
        Assert.Equal("ts=1980-01-01T10:05:00.000 event=request reason=\"first line\\r\\nsecond line\"", record);
    }

    [Fact]
    public void Format_UsesNoColumnPadding()
    {
        // Given values of wildly different widths.
        // When two records are formatted.
        var shortRecord = DiagnosticRecord.Format(Timestamp, "request", ("url", "http://a/"), ("status", "ok"));
        var longRecord = DiagnosticRecord.Format(Timestamp, "request", ("url", "http://a-very-much-longer-host.example.com/speedtest/random4000x4000.jpg?r=39"), ("status", "ok"));

        // Then fields are separated by exactly one space, so one long URL cannot reflow a snapshot.
        Assert.EndsWith("url=http://a/ status=ok", shortRecord);
        Assert.EndsWith(".jpg?r=39\" status=ok", longRecord);
    }

    [Fact]
    public void Record_StampsWithTheRecordersOwnClock_AndWritesInTheOrderRecorded()
    {
        // Given a recorder over a counting clock.
        using var writer = new StringWriter();
        var recorder = new BufferingDiagnosticRecorder(new IncrementingDiagnosticClockStub(), writer);

        // When two events are recorded and flushed.
        recorder.Record("run.start", ("version", "0.0.0"));
        recorder.Record("run.end", ("exit", "0"));
        recorder.Flush();

        // Then both appear, in order, each on its own line and each stamped as it was recorded.
        Assert.Equal(
            "ts=1980-01-01T10:05:00.000 event=run.start version=0.0.0" + Environment.NewLine +
            "ts=1980-01-01T10:05:05.000 event=run.end exit=0" + Environment.NewLine,
            writer.ToString());
    }

    [Fact]
    public void RecordAt_StampsWithTheTimeTheCallerSupplies()
    {
        // Given a recorder whose own clock would report a different time.
        using var writer = new StringWriter();
        var recorder = new BufferingDiagnosticRecorder(new IncrementingDiagnosticClockStub(), writer);

        // When a request is recorded with the time it started.
        recorder.RecordAt(new DateTime(1980, 1, 1, 11, 0, 7, 250), "request", ("seq", "3"));
        recorder.Flush();

        // Then the record carries that time, not the time the console received it.
        Assert.Equal("ts=1980-01-01T11:00:07.250 event=request seq=3" + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void Flush_CalledTwice_DoesNotRepeatRecordsAlreadyWritten()
    {
        // Given a run that flushes per iteration.
        using var writer = new StringWriter();
        var recorder = new BufferingDiagnosticRecorder(new IncrementingDiagnosticClockStub(), writer);

        // When a record is written, then another is recorded and flushed.
        recorder.Record("test.start", ("test", "download"));
        recorder.Flush();
        recorder.Record("test.end", ("test", "download"));
        recorder.Flush();

        // Then each record appears exactly once.
        Assert.Equal(1, CountOccurrences(writer.ToString(), "event=test.start"));
        Assert.Equal(1, CountOccurrences(writer.ToString(), "event=test.end"));
    }

    [Fact]
    public void NullRecorder_WritesNothingAndReportsItselfDisabled()
    {
        // Given the recorder in play when --diagnostics was not passed.
        var recorder = NullDiagnosticRecorder.Instance;

        // When events are recorded and flushed.
        recorder.Record("run.start", ("version", "0.0.0"));
        recorder.RecordAt(Timestamp, "request", ("seq", "1"));
        recorder.Flush();

        // Then it reports that nothing is being collected, so callers can skip the work.
        Assert.False(recorder.IsEnabled);
    }

    [Fact]
    public void Flush_WhenTheWriterFailsPartWay_KeepsTheUnwrittenRecordsAndRaisesTheFailure()
    {
        // Given a writer that accepts one line and then fails, as a filling disk would.
        using var writer = new FailingWriter(acceptLines: 1);
        var recorder = new BufferingDiagnosticRecorder(new IncrementingDiagnosticClockStub(), writer);

        recorder.Record("test.start", ("test", "download"));
        recorder.Record("request", ("seq", "1"));
        recorder.Record("request", ("seq", "2"));

        // When the flush fails part-way.
        Assert.Throws<IOException>(recorder.Flush);

        // Then the failure is raised rather than swallowed, and the records that never reached the
        // writer are still held - draining the buffer first and losing the tail would discard the
        // evidence silently, which is worse than the duplication it replaced.
        Assert.Single(writer.Written);

        writer.StopFailing();
        recorder.Flush();

        // And each record appears exactly once across both flushes: nothing lost, nothing doubled.
        Assert.Equal(3, writer.Written.Count);
        Assert.Equal(1, writer.Written.Count(line => line.Contains("seq=1")));
        Assert.Equal(1, writer.Written.Count(line => line.Contains("seq=2")));
    }

    /// <summary>
    /// A writer that fails after a set number of lines, then can be told to behave.
    /// </summary>
    private sealed class FailingWriter(int acceptLines) : StringWriter
    {
        private int remaining = acceptLines;
        private bool failing = true;

        public List<string> Written { get; } = [];

        public void StopFailing() => failing = false;

        public override void WriteLine(string? value)
        {
            if (failing && remaining <= 0)
            {
                throw new IOException("There is not enough space on the disk.");
            }

            remaining--;
            Written.Add(value ?? string.Empty);
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;

        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
