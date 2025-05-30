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
    /// The default value is 100,000 milliseconds (100 seconds), which matches the default timeout of <see cref="HttpClient"/>.
    /// See: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout
    /// </remarks>
    public int DefaultHttpTimeoutMilliseconds { get; init; } = 100000;

    /// <summary>
    /// The number of HTTP requests to send when measuring latency to a server.
    /// The average latency across these iterations will be used.
    /// </summary>
    public int LatencyTestIterations { get; init; } = 4;
}

