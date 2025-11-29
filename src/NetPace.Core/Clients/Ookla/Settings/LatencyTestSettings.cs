namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings used to configure how server latency is measured during a speed test.
/// </summary>
public sealed record LatencyTestSettings
{
    /// <summary>
    /// The timeout duration in milliseconds for each individual HTTP request when measuring latency.
    /// </summary>
    public int HttpTimeoutMilliseconds { get; init; } = 5000;

    /// <summary>
    /// The number of HTTP requests to send when measuring latency to a server.
    /// The average latency across these iterations will be used.
    /// </summary>
    public int LatencyTestIterations { get; init; } = 4;

    /// <summary>
    /// The delay in milliseconds between each latency test iteration.
    /// Set to 0 to disable delay between iterations.
    /// </summary>
    public int LatencyTestIntervalMilliseconds { get; init; } = 0;
}

