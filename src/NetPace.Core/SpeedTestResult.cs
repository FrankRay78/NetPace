namespace NetPace.Core;

/// <summary>
/// The speed test result.
/// </summary>
public sealed record SpeedTestResult
{
    /// <summary>
    /// Gets the total number of bytes processed.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the total elapsed time, in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Gets the number of requests that completed successfully and contributed to <see cref="BytesProcessed"/>.
    /// </summary>
    /// <remarks>
    /// Zero, with <see cref="RequestsFailed"/> greater than zero, means every request failed and the
    /// reported speed is not a valid measurement. Requests skipped because the configured byte
    /// budget was reached are counted as neither succeeded nor failed.
    /// </remarks>
    public int RequestsSucceeded { get; init; }

    /// <summary>
    /// Gets the number of requests that failed (a transport error, timeout, or non-success HTTP status).
    /// Failed requests contribute no bytes to <see cref="BytesProcessed"/>.
    /// </summary>
    public int RequestsFailed { get; init; }
}
