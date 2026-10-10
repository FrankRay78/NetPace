using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Helper class for selecting speed test servers.
/// </summary>
internal static class ServerSelector
{
    /// <summary>
    /// Gets the server to use for speed testing based on settings, and how it was chosen.
    /// </summary>
    public static async Task<ServerSelection> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        if (settings.NoLatency)
        {
            if (string.IsNullOrEmpty(settings.ServerUrl))
            {
                // Get the first speed test server.
                var servers = await speedTestClient.GetServersAsync(cancellationToken);
                if (servers.Length == 0)
                {
                    throw new Exception("No servers available");
                }
                var firstServer = servers.First();
                return new ServerSelection
                {
                    Result = new LatencyTestResult { Server = firstServer, LatencyMilliseconds = 0 },
                    Route = ServerSelection.FirstInListRoute
                };
            }
            else
            {
                // Create a minimal speed test server without testing latency.
                var server = new Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
                return new ServerSelection
                {
                    Result = new LatencyTestResult { Server = server, LatencyMilliseconds = 0 },
                    Route = ServerSelection.SpecifiedRoute
                };
            }
        }
        else
        {
            if (string.IsNullOrEmpty(settings.ServerUrl))
            {
                // Get the fastest speed test server.
                var servers = await speedTestClient.GetServersAsync(cancellationToken);
                if (servers.Length == 0)
                {
                    throw new Exception("No servers available");
                }
                // Screening chooses the server; the latency NetPace reports is then measured
                // properly, on the winner only. Once chosen, the server is not swapped out: if its
                // measurement fails, the run fails rather than falling back to another candidate.
                LatencyTestResult fastest;
                using (var screening = DiagnosticTestScope.For(recorder, DiagnosticTests.Screening))
                {
                    fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers, null, screening, cancellationToken);
                }

                using var measurement = DiagnosticTestScope.For(recorder, DiagnosticTests.Latency);
                return new ServerSelection
                {
                    Result = await speedTestClient.GetServerLatencyAsync(fastest.Server, null, measurement, cancellationToken),
                    Route = ServerSelection.AutoLatencyRoute,
                    ScreeningLatencyMilliseconds = fastest.LatencyMilliseconds
                };
            }
            else
            {
                // User specified speed test server.
                using var measurement = DiagnosticTestScope.For(recorder, DiagnosticTests.Latency);
                return new ServerSelection
                {
                    Result = await speedTestClient.GetServerLatencyAsync(settings.ServerUrl, null, measurement, cancellationToken),
                    Route = ServerSelection.SpecifiedRoute
                };
            }
        }
    }
}
