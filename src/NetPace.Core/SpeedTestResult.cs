namespace NetPace.Core;

/// <summary>
/// The speed test result, including the amount of data processed and the duration of the test.
/// </summary>
public sealed record SpeedTestResult
{
    /// <summary>
    /// Gets the total number of bytes processed during the speed test.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the total time taken to complete the speed test, in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }
}
