namespace NetPace.Console.Diagnostics;

/// <summary>
/// The recorder in play under <c>--diagnostics</c>: buffers formatted records and writes them to
/// the diagnostic stream on <see cref="Flush"/>.
/// </summary>
/// <remarks>
/// Requests run many-way parallel, so <see cref="Record"/> is called concurrently. Stamping,
/// formatting and appending all happen under one lock, which both keeps the buffer intact and fixes
/// the recorded order - sorting by timestamp would not restore it, and not only because the clock
/// can hand out the same instant twice: a request record carries the provider's clock while every
/// other record carries the diagnostic clock, so the two are not on one timeline at all.
/// <para>
/// Written order equals recorded order so long as one thread flushes. <see cref="Flush"/> writes
/// outside the lock, so two concurrent flushers could take disjoint batches and interleave them;
/// every caller today flushes from a single run loop.
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
        // meant a writer that threw part-way left every record still buffered, including the ones
        // already on disk, so the next flush wrote those again - a log with silently doubled lines
        // whose test.end totals no longer add up.
        //
        // Draining alone would swap that for the opposite fault, losing the unwritten tail with
        // nobody told, so whatever did not make it goes back on the front of the buffer and the
        // failure is raised. A later flush retries it; it is not discarded here, and it is not
        // this type's business whether the run survives a diagnostic write failing.
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
