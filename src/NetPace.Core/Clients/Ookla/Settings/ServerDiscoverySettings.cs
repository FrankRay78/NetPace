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
}
