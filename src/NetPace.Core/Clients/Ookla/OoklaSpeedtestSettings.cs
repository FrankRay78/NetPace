using System.Net;
using NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Configuration settings for the Ookla speed test implementation.
/// </summary>
public sealed record OoklaSpeedtestSettings
{
    // Test-specific settings

    /// <summary>
    /// Gets or sets the settings for discovering available speed test servers.
    /// </summary>
    public ServerDiscoverySettings ServerDiscovery { get; init; } = new();

    /// <summary>
    /// Gets or sets the settings for latency tests.
    /// </summary>
    public LatencyTestSettings LatencyTest { get; init; } = new();

    /// <summary>
    /// Gets or sets the settings for download speed tests.
    /// </summary>
    public DownloadTestSettings DownloadTest { get; init; } = new();

    /// <summary>
    /// Gets or sets the settings for upload speed tests.
    /// </summary>
    public UploadTestSettings UploadTest { get; init; } = new();

    // Network options

    /// <summary>
    /// Gets or sets the network credentials for proxy authentication.
    /// </summary>
    public NetworkCredential? ProxyCredential { get; init; }

    /// <summary>
    /// Gets or sets the proxy server address.
    /// </summary>
    public Uri? ProxyAddress { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to use a proxy for HTTP requests.
    /// </summary>
    public bool UseProxy { get; init; }
}
