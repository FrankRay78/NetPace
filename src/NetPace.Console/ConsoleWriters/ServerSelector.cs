using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

// Choosing and measuring are separate steps, in that order, and every output format takes them the
// same way, so a format that shows the server can name it while the first probe is still out
// (docs/architecture/server-screening-and-measurement.md). Once chosen, the server is never swapped
// out: if its measurement fails, the run fails rather than falling back to another candidate.
internal static class ServerSelector
{
    // Recorded here rather than in each of the four writers, and when the choice is made rather
    // than after the measurement, so a diagnostic log names the server even for a run whose
    // measurement then failed.
    public static async Task<ChosenServer> ChooseServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        // Bounded by the run's time limit as well as by the caller, so a candidate that never
        // answers cannot hold a scheduled run past it. Discovery and screening take one token.
        using var choosing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimitToken);

        ChosenServer chosen;
        try
        {
            chosen = await ChooseAsync(speedTestClient, settings, recorder, choosing.Token, timeLimitToken);
        }
        catch (OperationCanceledException) when (timeLimitToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Nothing to measure against, which is what NetPace already reports when no server can
            // be chosen: a run with no server reads the same whatever stopped the search.
            throw new NoServersAvailableException();
        }

        // latency_ms is the screening figure the choice was based on, absent where latency played no
        // part in choosing. The measured figure is in the latency test's own records.
        recorder.Record(
            "server.selected",
            ("sponsor", chosen.Server.Sponsor),
            ("location", chosen.Server.Location),
            ("url", chosen.Server.Url),
            ("selection", chosen.Route),
            ("latency_ms", chosen.ScreeningLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture)));

        return chosen;
    }

    // Null where nothing was measured - under --no-latency, or where the time limit was reached
    // before any probe answered - which the caller reports as a test that did not run.
    public static async Task<LatencyTestResult?> MeasureLatencyAsync(ISpeedTestService speedTestClient, ChosenServer chosen, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, IProgress<LatencyTestProgress>? progress = null, CancellationToken cancellationToken = default, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        if (settings.NoLatency || timeLimitToken.IsCancellationRequested)
        {
            return null;
        }

        using var measurement = DiagnosticTestScope.For(recorder, DiagnosticTests.Latency);

        try
        {
            // A server the user named is measured by URL, which is all the run was given: the
            // server object the choice carries is only the stand-in the screen names.
            return chosen.Route == ChosenServer.SpecifiedRoute
                ? await speedTestClient.GetServerLatencyAsync(chosen.Server.Url, progress, measurement, cancellationToken, timeLimitToken)
                : await speedTestClient.GetServerLatencyAsync(chosen.Server, progress, measurement, cancellationToken, timeLimitToken);
        }
        catch (OperationCanceledException) when (timeLimitToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // No probe answered, so there is no figure: a test that did not run.
            return null;
        }
    }

    // For a format that shows nothing until it has both; the steps are the same either way.
    public static async Task<(ChosenServer Chosen, LatencyTestResult? Latency)> GetServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default, CancellationToken timeLimitToken = default)
    {
        var chosen = await ChooseServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);
        var latency = await MeasureLatencyAsync(speedTestClient, chosen, settings, recorder, progress: null, cancellationToken, timeLimitToken);

        return (chosen, latency);
    }

    private static async Task<ChosenServer> ChooseAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken, CancellationToken timeLimitToken)
    {
        if (!string.IsNullOrEmpty(settings.ServerUrl))
        {
            // The placeholder sponsor and absent location are what the provider itself reports for
            // a server named by URL, so building it here leaves nothing to reconcile.
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
            // Nothing measured, so nothing to rank by: the first server offered is the one used.
            return new ChosenServer
            {
                Server = servers.First(),
                Route = ChosenServer.FirstInListRoute
            };
        }

        LatencyTestResult fastest;
        using (var screening = DiagnosticTestScope.For(recorder, DiagnosticTests.Screening, timeLimitToken))
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

}
