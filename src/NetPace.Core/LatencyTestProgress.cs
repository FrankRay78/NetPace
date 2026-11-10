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

    /// <summary>
    /// Gets the latency measured so far — the running figure behind
    /// <see cref="LatencyTestResult.LatencyMilliseconds"/> — or <see langword="null"/> where no
    /// probe has answered yet.
    /// </summary>
    /// <remarks>
    /// The mean of the probes that have answered, so the figure a consumer watches ends on the
    /// figure the result reports — both are the mean of the same probes.
    /// <see langword="null"/> says nothing measured yet rather than zero.
    /// </remarks>
    public long? LatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets how uneven the probes behind <see cref="LatencyMilliseconds"/> have been so far, in
    /// milliseconds, or <see langword="null"/> where there is nothing yet to report.
    /// </summary>
    /// <remarks>
    /// Unevenness is a change from one round trip to the next, so this stays absent until a second
    /// probe has answered: a single probe has no consecutive pair to compare, and reports no figure
    /// rather than a zero. <see cref="LatencyTestResult.JitterMilliseconds"/> is a plain
    /// <see langword="long"/> and cannot say that, so a measurement of one probe alone reports no
    /// jitter here and zero there.
    /// </remarks>
    public long? JitterMilliseconds { get; init; }
}
