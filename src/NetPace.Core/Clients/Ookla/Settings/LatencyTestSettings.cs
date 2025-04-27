namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for measuring server latency.
/// </summary>
public sealed record LatencyTestSettings
{
    // The default timeout for HttpClient is 100 seconds.
    // ref: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.timeout?view=net-9.0
    public int DefaultHttpTimeoutMilliseconds { get; set; } = 100000;

    public int LatencyTestIterations { get; set; } = 4;
}
