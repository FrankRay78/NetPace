using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Choosing the speed test server a run will use, and then measuring its latency.
/// </summary>
/// <remarks>
/// The two are separate steps, in that order, and every output format takes them the same way:
/// the choice is made and recorded first, so a format that shows the server can name it while the
/// first probe is still out, and the measurement follows against a server that is already
/// settled. Once chosen, the server is not swapped out: if its measurement fails, the run fails
/// rather than falling back to another candidate.
/// </remarks>
internal static class ServerSelector
{
    /// <summary>
    /// Chooses the server to use, by whichever route the settings ask for, and records the choice.
    /// </summary>
    /// <remarks>
    /// Recorded here rather than in each of the four writers: they all want the same record, and
    /// none of them needs the route for anything else. Written when the choice is made rather than
    /// after the measurement, so a diagnostic log names the server even for a run whose
    /// measurement then failed.
    /// </remarks>
    public static async Task<ChosenServer> ChooseServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        var chosen = await ChooseAsync(speedTestClient, settings, recorder, cancellationToken);

        // latency_ms is the screening figure, because that is what the choice was based on, and is
        // absent where latency played no part in choosing - under --no-latency, or when the user
        // named the server. The measured figure appears in the latency test's own records.
        recorder.Record(
            "server.selected",
            ("sponsor", chosen.Server.Sponsor),
            ("location", chosen.Server.Location),
            ("url", chosen.Server.Url),
            ("selection", chosen.Route),
            ("latency_ms", chosen.ScreeningLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture)));

        return chosen;
    }

    /// <summary>
    /// Measures the latency of the server already chosen, reporting progress to
    /// <paramref name="progress"/> where one was given.
    /// </summary>
    /// <returns>
    /// The measured figures, or, under <c>--no-latency</c>, a result stating that nothing was
    /// measured.
    /// </returns>
    public static async Task<LatencyTestResult> MeasureLatencyAsync(ISpeedTestService speedTestClient, ChosenServer chosen, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, IProgress<LatencyTestProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        if (settings.NoLatency)
        {
            return Unmeasured(chosen.Server);
        }

        using var measurement = DiagnosticTestScope.For(recorder, DiagnosticTests.Latency);

        // A server the user named is measured by URL, which is all the run was ever given: the
        // server object the choice carries is the stand-in a provider builds from that same URL,
        // there so the screen can name what is being tested before the first probe answers.
        return chosen.Route == ChosenServer.SpecifiedRoute
            ? await speedTestClient.GetServerLatencyAsync(chosen.Server.Url, progress, measurement, cancellationToken)
            : await speedTestClient.GetServerLatencyAsync(chosen.Server, progress, measurement, cancellationToken);
    }

    /// <summary>
    /// Chooses the server and measures its latency, for a format that shows nothing until it has
    /// both. The two steps are the same ones a format that shows the server first takes.
    /// </summary>
    public static async Task<LatencyTestResult> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default)
    {
        var chosen = await ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken);

        return await MeasureLatencyAsync(speedTestClient, chosen, settings, recorder, progress: null, cancellationToken);
    }

    private static async Task<ChosenServer> ChooseAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(settings.ServerUrl))
        {
            // The user named the server, so there is nothing to choose. The placeholder sponsor and
            // absent location are what the provider itself reports for a server named by URL, so
            // building it here rather than inside the measurement leaves nothing to reconcile.
            return new ChosenServer
            {
                Server = new Server { Sponsor = Server.UnknownSponsor, Url = settings.ServerUrl },
                Route = ChosenServer.SpecifiedRoute
            };
        }

        var servers = await speedTestClient.GetServersAsync(cancellationToken);
        if (servers.Length == 0)
        {
            throw new NoServersAvailableException();
        }

        if (settings.NoLatency)
        {
            // Nothing was measured, so there is nothing to rank by: the first server offered is
            // the one used.
            return new ChosenServer
            {
                Server = servers.First(),
                Route = ChosenServer.FirstInListRoute
            };
        }

        // Screening chooses the server; the latency NetPace reports is then measured properly, on
        // the winner only.
        LatencyTestResult fastest;
        using (var screening = DiagnosticTestScope.For(recorder, DiagnosticTests.Screening))
        {
            fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers, null, screening, cancellationToken);
        }

        return new ChosenServer
        {
            Server = fastest.Server,
            Route = ChosenServer.AutoLatencyRoute,
            ScreeningLatencyMilliseconds = fastest.LatencyMilliseconds
        };
    }

    /// <summary>
    /// A server used without measuring its latency, under <c>--no-latency</c>: no probe time was
    /// recorded, so the extremes say not recorded and the figures that do read as a number are
    /// zero.
    /// </summary>
    private static LatencyTestResult Unmeasured(IServer server) =>
        new() { Server = server, LatencyMilliseconds = 0, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 0 };
}
