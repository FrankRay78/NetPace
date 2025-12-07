namespace NetPace.Core;

/// <summary>
/// The progress update for an inflight speed test.
/// </summary>
public sealed record SpeedTestProgress
{
    /// <summary>
    /// Gets the percentage complete.
    /// </summary>
    public int PercentageComplete { get; init; }

    /// <summary>
    /// Gets the total number of bytes processed.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the total elapsed time, in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }
}
