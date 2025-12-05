namespace NetPace.Core;

/// <summary>
/// The latency test result for a specific server.
/// </summary>
public sealed record LatencyTestResult
{
    /// <summary>
    /// Gets the server that was tested.
    /// </summary>
    public required IServer Server { get; init; }

    /// <summary>
    /// Gets the measured latency to the server, in milliseconds.
    /// </summary>
    public long LatencyMilliseconds { get; init; }
}
