namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for discovering available speed test servers.
/// </summary>
public sealed class ServerDiscoverySettings
{
    public string ServersUrl { get; set; } = "http://www.speedtest.net/speedtest-servers.php";
}
