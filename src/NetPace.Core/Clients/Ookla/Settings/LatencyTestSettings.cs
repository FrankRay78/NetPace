namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings used to configure how server latency is measured during a speed test.
/// </summary>
public sealed record LatencyTestSettings
{
    /// <summary>
    /// The timeout duration in milliseconds for each individual HTTP request when measuring latency.
    /// </summary>
    /// <remarks>
    /// The default value is 5,000 milliseconds (5 seconds), which provides a reasonable timeout
    /// for latency tests while preventing excessive waits for unresponsive servers.
    /// </remarks>
    public int DefaultHttpTimeoutMilliseconds { get; init; } = 5000;

    /// <summary>
    /// The number of HTTP requests to send when measuring latency to a server.
    /// The average latency across these iterations will be used.
    /// </summary>
    public int LatencyTestIterations { get; init; } = 10;

    /// <summary>
    /// The delay in milliseconds between each latency test iteration.
    /// Set to 0 to disable delay between iterations.
    /// </summary>
    public int LatencyTestIntervalMilliseconds { get; init; } = 100;
}

