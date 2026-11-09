namespace NetPace.Core;

/// <summary>
/// The speed test result.
/// </summary>
/// <remarks>
/// Every property is <c>required</c>, so no value this record reports can be silently defaulted —
/// each must be stated at construction. That is the whole of the guarantee: a stated value is not
/// thereby a measured one, and it is the counts that distinguish a genuine measurement from a run
/// that failed (see <see cref="RequestsSucceeded"/>). Where a test did not run at all, represent
/// that as the absence of a result; an all-failed test reports a zero speed, not an absent result.
/// </remarks>
public sealed record SpeedTestResult
{
    /// <summary>
    /// Gets the total number of bytes processed.
    /// </summary>
    public required long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the total elapsed time, in milliseconds.
    /// </summary>
    public required long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Gets the number of requests that completed successfully and contributed to <see cref="BytesProcessed"/>.
    /// </summary>
    /// <remarks>
    /// Zero, with <see cref="RequestsFailed"/> greater than zero, means every request failed and the
    /// reported speed is not a valid measurement. Requests skipped because the configured byte
    /// budget was reached are counted as neither succeeded nor failed.
    /// </remarks>
    public required int RequestsSucceeded { get; init; }

    /// <summary>
    /// Gets the number of requests that failed (a transport error, timeout, or non-success HTTP status).
    /// Failed requests contribute no bytes to <see cref="BytesProcessed"/>.
    /// </summary>
    public required int RequestsFailed { get; init; }

    /// <summary>
    /// Gets the latency measured while this transfer was running — latency under load — or
    /// <see langword="null"/> where it was not measured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The figures come from small probes sent to the same server while the transfer ran:
    /// <see cref="LatencyTestResult.LatencyMilliseconds"/> is their plain mean, so it compares
    /// directly with an idle measurement, and the lowest, highest and jitter are of the same
    /// samples. The probes' bytes and durations are not part of <see cref="BytesProcessed"/> or
    /// <see cref="ElapsedMilliseconds"/>, so they never enter the reported speed.
    /// </para>
    /// <para>
    /// <see langword="null"/> says not measured, which is not the same as zero: the measurement was
    /// switched off, the transfer ended before the first probe was due, no probe answered before it
    /// ended, or every one of the transfer's own requests failed, so the probes never saw a loaded
    /// line.
    /// </para>
    /// </remarks>
    public required LatencyTestResult? Latency { get; init; }
}
