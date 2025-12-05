namespace NetPace.Core;

/// <summary>
/// The progress update for an inflight latency test.
/// </summary>
public sealed record LatencyTestProgress
{
    /// <summary>
    /// Gets the list of individual ping results in milliseconds.
    /// </summary>
    public List<long> Pings { get; init; } = [];

    /// <summary>
    /// Gets the percentage complete.
    /// </summary>
    public int PercentageComplete { get; init; }
}
