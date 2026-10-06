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
    /// A candidate that has not answered by the ceiling is treated as unreachable and drops out.
    /// </remarks>
    public int ServerTimeoutMilliseconds { get; init; } = 2000;

    /// <summary>
    /// The number of requests sent to each server when screening it, with no deliberate waiting
    /// between them. The fastest of the requests that completed is the server's screening latency.
    /// </summary>
    /// <remarks>
    /// Screening only has to rank candidates, so it is deliberately far cheaper than the full
    /// latency measurement taken of the winner afterwards
    /// (<see cref="LatencyTestSettings.LatencyTestIterations"/>).
    /// </remarks>
    public int ScreeningRequestCount { get; init; } = 3;
}
