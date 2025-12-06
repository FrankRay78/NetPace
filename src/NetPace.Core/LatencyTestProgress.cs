namespace NetPace.Core;

/// <summary>
/// The progress update for an inflight latency test.
/// </summary>
public sealed record LatencyTestProgress
{
    /// <summary>
    /// Gets the percentage complete.
    /// </summary>
    public int PercentageComplete { get; init; }
}
