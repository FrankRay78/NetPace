namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for discovering available speed test servers.
/// </summary>
public sealed record ServerDiscoverySettings
{
    public string ServersUrl { get; init; } = "http://www.speedtest.net/speedtest-servers.php";
}
