using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Helper class for selecting speed test servers.
/// </summary>
internal static class ServerSelector
{
    /// <summary>
    /// Gets the server to use for speed testing based on settings, recording the chosen server and
    /// the route that chose it.
    /// </summary>
    public static async Task<LatencyTestResult> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        // Recorded here rather than in each of the four writers: they all want the same record, and
        // none of them needs the route for anything else. The selection's own test scopes have
        // closed by the time the choice is known, so this record lands below them.
        var selection = await SelectServerAsync(speedTestClient, settings, recorder, cancellationToken);

        // latency_ms is the screening figure, because that is what the choice was based on, and is
        // absent where latency played no part in choosing - under --no-latency, or when the user
        // named the server. The measured figure appears in the latency test's own records.
        recorder.Record(
            "server.selected",
            ("sponsor", selection.Result.Server.Sponsor),
            ("location", selection.Result.Server.Location),
            ("url", selection.Result.Server.Url),
            ("selection", selection.Route),
            ("latency_ms", selection.ScreeningLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture)));

        return selection.Result;
    }

    private static async Task<ServerSelection> SelectServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken)
    {
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
