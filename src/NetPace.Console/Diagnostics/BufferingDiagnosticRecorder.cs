namespace NetPace.Console.Diagnostics;

/// <summary>
/// The recorder in play whenever diagnostics are on: buffers formatted records and writes them on
/// <see cref="Flush"/> to the writer it was given, which is the diagnostic stream under
/// <c>--diagnostics</c> and the named file under <c>--diagnostics-file</c>.
/// </summary>
/// <remarks>
/// Requests run many-way parallel, so this type does not assume a single caller even though
/// <c>DiagnosticTestScope</c> happens to serialise it today. Stamping, formatting and appending
/// happen under one lock, which fixes the recorded order: sorting by timestamp could not restore
/// it, because a request record carries the provider's clock while every other record carries the
/// diagnostic clock, and under the test clocks those are not one timeline.
/// <para>
/// <see cref="Flush"/> writes outside the lock, so two concurrent flushers could take disjoint
/// batches and interleave them; every caller today flushes from a single run loop.
/// </para>
/// </remarks>
public sealed class BufferingDiagnosticRecorder(IDiagnosticClock clock, TextWriter writer) : IDiagnosticRecorder
{
    private readonly List<string> records = [];
    private readonly object recordLock = new();

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public void Record(string eventName, params (string Key, string? Value)[] fields)
    {
        lock (recordLock)
        {
            records.Add(DiagnosticRecord.Format(clock.Now, eventName, fields));
        }
    }

    /// <inheritdoc />
    public void RecordAt(DateTime timestamp, string eventName, params (string Key, string? Value)[] fields)
    {
        lock (recordLock)
        {
            records.Add(DiagnosticRecord.Format(timestamp, eventName, fields));
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        // Taken off the buffer before anything is written, not cleared after: clearing afterwards
        // left every record buffered when a writer threw part-way, including the ones already on
        // disk, and the next flush wrote those again. Whatever did not make it goes back on the
        // front of the buffer and the failure is raised, so a later flush retries it rather than
        // losing the tail with nobody told.
        List<string> pending;

        lock (recordLock)
        {
            pending = [.. records];
            records.Clear();
        }

        var written = 0;

        try
        {
            foreach (string record in pending)
            {
                writer.WriteLine(record);
                written++;
            }

            writer.Flush();
        }
        catch
        {
            lock (recordLock)
            {
                records.InsertRange(0, pending.Skip(written));
            }

            throw;
        }
    }
}
