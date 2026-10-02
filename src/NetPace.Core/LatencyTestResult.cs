namespace NetPace.Core;

/// <summary>
/// The latency test result for a specific server.
/// </summary>
/// <remarks>
/// Every property is <see langword="required"/>: a result reports measured values, so omitting one
/// would publish a default that is indistinguishable from a real measurement.
/// </remarks>
public sealed record LatencyTestResult
{
    /// <summary>
    /// Gets the server that was tested.
    /// </summary>
    public required IServer Server { get; init; }

    /// <summary>
    /// Gets the measured latency to the server, in milliseconds.
    /// </summary>
    public required long LatencyMilliseconds { get; init; }
}
