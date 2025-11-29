namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for discovering available speed test servers.
/// </summary>
public sealed record ServerDiscoverySettings
{
    /// <summary>
    /// Gets or sets the URL to retrieve the list of available Speedtest servers.
    /// </summary>
    /// <remarks>
    /// Defaults to the official Speedtest.net server list endpoint.
    /// </remarks>
    public string ServersUrl { get; init; } = "http://www.speedtest.net/speedtest-servers.php";

    /// <summary>
    /// The timeout duration in milliseconds when probing a server.
    /// </summary>
    public int ServerTimeoutMilliseconds { get; init; } = 2000;

    /// <summary>
    /// The number of ping requests to send when probing a server.
    /// </summary>
    public int PingIterations { get; init; } = 4;

    /// <summary>
    /// The delay in milliseconds between each server probe.
    /// Set to 0 to disable delay between iterations.
    /// </summary>
    public int PingIntervalMilliseconds { get; init; } = 0;
}
