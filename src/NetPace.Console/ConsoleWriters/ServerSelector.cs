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
    public static async Task<ServerLatencyResult> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        if (settings.NoLatency)
        {
            if (string.IsNullOrEmpty(settings.ServerUrl))
            {
                // Get the first speed test server.
                var servers = await speedTestClient.GetServersAsync(cancellationToken);
                var firstServer = servers.First();
                return new ServerLatencyResult { Server = firstServer, Latency = 0 };
            }
            else
            {
                // Create a minimal speed test server without testing latency.
                var server = new Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
                return new ServerLatencyResult { Server = server, Latency = 0 };
            }
        }
        else
        {
            if (string.IsNullOrEmpty(settings.ServerUrl))
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
}
