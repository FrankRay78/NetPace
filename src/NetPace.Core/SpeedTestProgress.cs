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

    /// <summary>
    /// Gets the reason a single request failed, when this update announces a per-request failure;
    /// otherwise <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Per-request failures are streamed live on the same progress channel that drives the progress
    /// bar so consumers can surface them (for example, at a diagnostic verbosity level) as they
    /// happen. The reason is not retained on <see cref="SpeedTestResult"/>; only the request counts are.
    /// </remarks>
    public string? FailedRequestReason { get; init; }
}
