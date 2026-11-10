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
    /// has been ranked inside the first couple of seconds.
    /// Must be positive; a non-positive value is rejected when selection runs, not at construction.
    /// <para>
    /// Ten seconds, so that a link whose round trip is a second or two can still have a server
    /// chosen for it. The ceiling is reached in stages rather than waited out: a pass that has
    /// ranked a candidate inside the first couple of seconds takes it and moves on, so a run on a
    /// quick link chooses a server as fast as it ever did, and only a pass that has ranked nothing
    /// waits for the rest. A ceiling above two seconds therefore bounds how long a pass that has
    /// ranked nothing waits; it does not give a slower candidate the whole ceiling once a quicker
    /// one has been ranked. See
    /// <c>docs/architecture/server-screening-and-measurement.md</c>.
    /// </para>
    /// </remarks>
    public int ServerTimeoutMilliseconds { get; init; } = 10000;

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
