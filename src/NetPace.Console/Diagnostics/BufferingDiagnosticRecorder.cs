namespace NetPace.Console.Diagnostics;

/// <summary>
/// The recorder in play whenever diagnostics are on: buffers formatted records and writes them on
/// <see cref="Flush"/> to the writer it was given, which is the diagnostic stream under
/// <c>--diagnostics</c> and the named file under <c>--diagnostics-file</c>.
/// </summary>
/// <remarks>
/// Requests run many-way parallel, so no single caller is assumed and one lock fixes the recorded
/// order: sorting by timestamp would not restore it, two clocks being in play.
/// <para>
/// <see cref="Flush"/> writes outside the lock, so written order equals recorded order only while
/// one thread flushes; every caller today flushes from a single run loop.
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
        // Drained before writing, not cleared after: clearing after a part-way failure rewrote
        // the records already on disk. The unwritten tail goes back on the front and raises.
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
