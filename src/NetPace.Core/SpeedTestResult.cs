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
    /// Gets the number of individual requests attempted during the test (successes plus failures).
    /// </summary>
    /// <remarks>
    /// Requests skipped because the configured byte budget was reached are not counted. A consumer
    /// determines whether a dimension produced any valid measurement via
    /// <see cref="RequestsSucceeded"/>; a value of zero with <see cref="RequestsAttempted"/> greater
    /// than zero means every request failed and the reported speed is not a valid measurement.
    /// </remarks>
    public int RequestsAttempted { get; init; }

    /// <summary>
    /// Gets the number of requests that completed successfully and contributed to <see cref="BytesProcessed"/>.
    /// </summary>
    public int RequestsSucceeded { get; init; }

    /// <summary>
    /// Gets the number of requests that failed (a transport error, timeout, or non-success HTTP status).
    /// Failed requests contribute no bytes to <see cref="BytesProcessed"/>.
    /// </summary>
    public int RequestsFailed { get; init; }
}
