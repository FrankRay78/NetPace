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
    /// <returns>
    /// The selected server and its latency, or <see langword="null"/> when discovery returns no
    /// usable server or the discovery endpoint cannot be reached. A missing server is a reported
    /// data outcome, not an error — only caller-requested cancellation propagates.
    /// </returns>
    public static async Task<LatencyTestResult?> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);

        // Obtaining a server is part of the discovery/selection region, so a network failure here
        // (an unreachable discovery endpoint, a user-specified host that does not respond to the
        // latency probe, or all latency probes failing) is data, not an error: it is reported as
        // "no usable server" rather than thrown. Only caller-requested cancellation propagates.
        try
        {
            if (!string.IsNullOrEmpty(settings.ServerUrl))
            {
                if (settings.NoLatency)
                {
                    // Create a minimal speed test server without testing latency.
                    var server = new Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
                    return new LatencyTestResult { Server = server, LatencyMilliseconds = 0 };
                }

                // User specified speed test server.
                return await speedTestClient.GetServerLatencyAsync(settings.ServerUrl, cancellationToken);
            }

            var servers = await speedTestClient.GetServersAsync(cancellationToken);
            if (servers.Length == 0)
            {
                return null;
            }

            if (settings.NoLatency)
            {
                return new LatencyTestResult { Server = servers.First(), LatencyMilliseconds = 0 };
            }

            return await speedTestClient.GetFastestServerByLatencyAsync(servers, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-requested cancellation always propagates.
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
