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
    public static async Task<ChosenServer> ChooseServerAsync(ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken = default, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(speedTestClient);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(recorder);

        // Bounded by the run's time limit too, so a candidate that never answers cannot hold a
        // scheduled run past it.
        using var choosing = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeLimitToken);

        ChosenServer chosen;
        try
        {
            chosen = await ChooseAsync(speedTestClient, settings, recorder, choosing.Token, timeLimitToken);
        }
        catch (OperationCanceledException) when (timeLimitToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // Nothing to measure against, which is the condition NetPace already reports when no
            // server can be chosen: a run with no server reads the same whatever stopped it.
            throw new NoServersAvailableException();
        }

        // The screening figure, because that is what the choice was based on; absent where latency
        // played no part in it.
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
    /// The measured figures, or <see langword="null"/> where nothing was measured: under
    /// <c>--no-latency</c>, or where the run's time limit was reached before any probe answered.
    /// Either way the caller reports the test as one that did not run.
    /// </returns>
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
            // Measured by URL, which is all the run was given; the server object is a stand-in so
            // the screen can name what is being tested.
            return chosen.Route == ChosenServer.SpecifiedRoute
                ? await speedTestClient.GetServerLatencyAsync(chosen.Server.Url, progress, measurement, cancellationToken, timeLimitToken)
                : await speedTestClient.GetServerLatencyAsync(chosen.Server, progress, measurement, cancellationToken, timeLimitToken);
        }
        catch (OperationCanceledException) when (timeLimitToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // The limit was reached with no probe answered, so there is no figure to report. A
            // measurement that took no probes is a test that did not run, and is reported as one.
            return null;
        }
    }

    /// <summary>
    /// Chooses the server and measures its latency, for a format that shows nothing until it has
    /// both. The two steps are the same ones a format that shows the server first takes.
    /// </summary>
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
