using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Helper class for selecting speed test servers.
/// </summary>
internal static class ServerSelector
{
    /// <summary>
    /// Gets the server to use for speed testing based on settings.
    /// </summary>
    /// <param name="speedTestClient">The speed test service.</param>
    /// <param name="settings">The command settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ServerLatencyResult containing the selected server and latency (0 if latency test was skipped).</returns>
    public static async Task<ServerLatencyResult> GetServerAsync(
        ISpeedTestService speedTestClient,
        SpeedTestCommandSettings settings,
        CancellationToken cancellationToken)
    {
        if (settings.NoLatency)
        {
            // Skip latency test - use first available server or specified server
            if (string.IsNullOrEmpty(settings.ServerUrl))
            {
                var servers = await speedTestClient.GetServersAsync(cancellationToken);
                var firstServer = servers.First();
                return new ServerLatencyResult { Server = firstServer, Latency = 0 };
            }
            else
            {
                // User specified server - create a minimal ServerLatencyResult without testing latency
                var servers = await speedTestClient.GetServersAsync(cancellationToken);
                var specifiedServer = servers.FirstOrDefault(s => s.Url == settings.ServerUrl);
                if (specifiedServer == null)
                {
                    // If not in list, create a basic server object
                    specifiedServer = new NetPace.Core.Clients.Ookla.Server
                    {
                        Url = settings.ServerUrl,
                        Sponsor = "Unknown",
                        Location = "Unknown"
                    };
                }
                return new ServerLatencyResult { Server = specifiedServer, Latency = 0 };
            }
        }
        else if (string.IsNullOrEmpty(settings.ServerUrl))
        {
            // Get the fastest speed test server.
            var servers = await speedTestClient.GetServersAsync(cancellationToken);
            return await speedTestClient.GetFastestServerByLatencyAsync(servers, cancellationToken);
        }
        else
        {
            // User specified speed test server.
            return await speedTestClient.GetServerLatencyAsync(settings.ServerUrl, cancellationToken);
        }
    }
}
