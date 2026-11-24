namespace NetPace.Core.Settings;

/// <summary>
/// Settings for screening candidate speed test servers to choose one.
/// </summary>
/// <remarks>
/// Where the list itself comes from is not provider-neutral: a provider that discovers servers
/// names its own feed on its own settings record.
/// </remarks>
public sealed record ServerScreeningSettings
{
    /// <summary>
    /// The overall ceiling in milliseconds on screening the discovered servers to choose one.
    /// </summary>
    /// <remarks>
    /// A ceiling on the whole pass, not a budget per server: candidates are screened concurrently,
    /// so the worst case is roughly one timeout rather than one per server. A candidate that has not
    /// answered by the time the pass ends drops out as unreachable. Must be positive; a non-positive
    /// value is rejected when selection runs, not at construction.
    /// <para>
    /// The ceiling is spent in stages, so it bounds how long a pass that has ranked nothing waits
    /// rather than giving a slower candidate the whole ceiling once a quicker one has been ranked.
    /// To give every candidate the whole ceiling, set
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
    /// <see cref="ScreeningRequestCount"/> of its requests have returned. Lower it to choose sooner;
    /// raise it to let slower candidates compete beside quicker ones, or set it at or above the
    /// ceiling to spend the ceiling in a single stage. Must be positive; a non-positive value is
    /// rejected when selection runs, not at construction.
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
