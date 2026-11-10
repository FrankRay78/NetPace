namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for discovering available speed test servers.
/// </summary>
public sealed record ServerDiscoverySettings
{
    /// <summary>
    /// Gets or sets the URL to retrieve the list of available Speedtest servers.
    /// </summary>
    /// <remarks>
    /// Defaults to the official Speedtest.net server list endpoint.
    /// </remarks>
    public string ServersUrl { get; init; } = "http://www.speedtest.net/speedtest-servers.php";

    /// <summary>
    /// The overall ceiling in milliseconds on screening the discovered servers to choose one.
    /// </summary>
    /// <remarks>
    /// This is a ceiling on the whole screening pass, not a budget per server: candidates are
    /// screened concurrently, so the worst case is roughly one timeout rather than one per server.
    /// A candidate that has not answered by the time the pass ends is treated as unreachable and
    /// drops out; the pass ends at the ceiling at the latest, and sooner when another candidate
    /// has been ranked by the end of the first stage, <see cref="FirstScreeningStageMilliseconds"/>.
    /// Must be positive; a non-positive value is rejected when selection runs, not at construction.
    /// <para>
    /// Ten seconds, so that a link whose round trip is a second or two can still have a server
    /// chosen for it. The ceiling is reached in stages rather than waited out: a pass that has
    /// ranked a candidate by the end of the first stage takes it and moves on, so a run on a quick
    /// link chooses a server as fast as it ever did, and only a pass that has ranked nothing waits
    /// for the rest. A ceiling above the first stage therefore bounds how long a pass that has
    /// ranked nothing waits; it does not give a slower candidate the whole ceiling once a quicker
    /// one has been ranked. To give every candidate the whole ceiling, set
    /// <see cref="FirstScreeningStageMilliseconds"/> to the ceiling as well. See
    /// <c>docs/architecture/server-screening-and-measurement.md</c>.
    /// </para>
    /// </remarks>
    public int ServerTimeoutMilliseconds { get; init; } = 10000;

    /// <summary>
    /// How long in milliseconds a screening pass waits before it settles for a candidate it has
    /// already ranked: the first stage of <see cref="ServerTimeoutMilliseconds"/>.
    /// </summary>
    /// <remarks>
    /// A pass that has ranked a candidate by the end of this stage chooses the fastest one ranked
    /// and abandons the rest; only a pass that has ranked nothing waits on, to the ceiling, for
    /// every candidate to finish. A candidate counts as ranked once all
    /// <see cref="ScreeningRequestCount"/> of its requests have returned.
    /// <para>
    /// Two seconds, which was the whole ceiling before the ceiling was raised for slow links, so a
    /// run on a quick link chooses a server exactly as fast as it did then. Lower it to choose
    /// sooner; raise it to let slower candidates compete beside quicker ones. A value at or above
    /// the ceiling spends the ceiling in a single stage, so every candidate is given the whole
    /// ceiling, as it was before the ceiling was spent in stages.
    /// </para>
    /// Must be positive; a non-positive value is rejected when selection runs, not at construction.
    /// </remarks>
    public int FirstScreeningStageMilliseconds { get; init; } = 2000;

    /// <summary>
    /// The number of requests sent to each server when screening it, with no deliberate waiting
    /// between them. The fastest of the requests that completed is the server's screening latency.
    /// </summary>
    /// <remarks>
    /// Screening only has to rank candidates, so it is deliberately far cheaper than the full
    /// latency measurement taken of the winner afterwards
    /// (<see cref="LatencyTestSettings.LatencyTestIterations"/>).
    /// Must be positive; a non-positive value is rejected when selection runs, not at construction.
    /// </remarks>
    public int ScreeningRequestCount { get; init; } = 3;
}
