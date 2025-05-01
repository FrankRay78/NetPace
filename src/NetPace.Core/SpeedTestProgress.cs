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
}
