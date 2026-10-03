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
}
