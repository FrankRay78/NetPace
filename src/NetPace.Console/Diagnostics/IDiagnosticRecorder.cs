namespace NetPace.Console.Diagnostics;

/// <summary>
/// Collects diagnostic records during a run and writes them out afterwards.
/// </summary>
/// <remarks>
/// Buffered rather than written as it happens: console I/O per request would perturb the very
/// numbers being measured, and interleaving with the live progress display shreds it (#245).
/// </remarks>
public interface IDiagnosticRecorder
{
    /// <summary>
    /// Gets whether anything is being recorded. <see langword="false"/> under the null recorder, so
    /// a caller can skip assembling a record it knows will be discarded.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Records one event, stamped with the recorder's own clock. A field whose value is
    /// <see langword="null"/> is omitted.
    /// </summary>
    void Record(string eventName, params (string Key, string? Value)[] fields);

    /// <summary>
    /// Records one event stamped with a time the caller already knows.
    /// </summary>
    /// <remarks>
    /// A request record carries the time the request started, which only the provider that issued
    /// it knows; stamping it on receipt would report when the console got round to it instead.
    /// </remarks>
    void RecordAt(DateTime timestamp, string eventName, params (string Key, string? Value)[] fields);

    /// <summary>
    /// Writes every record collected so far, in the order they were recorded, and clears the buffer.
    /// </summary>
    /// <remarks>
    /// May throw if the underlying stream cannot be written. Whatever did not reach the stream
    /// stays buffered for a later flush to retry, so a failure loses no record.
    /// </remarks>
    void Flush();
}
