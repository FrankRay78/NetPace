namespace NetPace.Core.Settings;

/// <summary>
/// Settings used to configure how server latency is measured during a speed test.
/// </summary>
public sealed record LatencyTestSettings
{
    /// <summary>
    /// The timeout duration in milliseconds for each individual HTTP request of the idle latency
    /// measurement.
    /// </summary>
    /// <remarks>
    /// Ten seconds, so that a link whose round trip is a second or two - a geostationary satellite,
    /// a congested mobile cell, 2G data - is measured rather than refused, with room for one
    /// retransmit on top of the round trip itself. A probe that does not answer inside it still ends
    /// the measurement: the limit is long enough for a slow link, and is still a limit. It bounds
    /// one probe and nothing bounds the run of them, so the measurement's worst case is this limit
    /// multiplied by <see cref="LatencyTestIterations"/> - with the defaults, a server answering
    /// just inside the limit every time occupies the latency stage for some minutes.
    /// <para>
    /// Must be positive; a non-positive value, including <see cref="Timeout.Infinite"/>, is rejected
    /// when latency is measured, not at construction. It does not bound the under-load probes a
    /// download or an upload sends: a probe that takes a long time under load is the measurement
    /// rather than a fault, so those carry no timeout beyond the HTTP client's own.
    /// </para>
    /// </remarks>
    public int HttpTimeoutMilliseconds { get; init; } = 10000;

    /// <summary>
    /// The number of HTTP requests to send when measuring idle latency to a server.
    /// The average latency across these iterations will be used.
    /// </summary>
    public int LatencyTestIterations { get; init; } = 20;

    /// <summary>
    /// The delay in milliseconds between each idle latency test iteration.
    /// Set to 0 to disable delay between iterations.
    /// </summary>
    public int LatencyTestIntervalMilliseconds { get; init; } = 50;

    /// <summary>
    /// Whether a download or an upload measures the latency to the server while it runs — latency
    /// under load. On by default.
    /// </summary>
    /// <remarks>
    /// Switching this off is the only way to stop the probes; a zero or negative
    /// <see cref="LoadedLatencyIntervalMilliseconds"/> is rejected rather than read as "do not
    /// probe", because an interval of none would probe continuously.
    /// </remarks>
    public bool LoadedLatencyEnabled { get; init; } = true;

    /// <summary>
    /// The pause between the under-load latency probes a download or an upload sends, in
    /// milliseconds. The first probe goes out one of these after the transfer starts.
    /// </summary>
    /// <remarks>
    /// Named apart from <see cref="LatencyTestIntervalMilliseconds"/>, which paces the idle
    /// measurement: the two are different measurements and their intervals are set independently.
    /// Must be positive; a non-positive value is rejected when a download or an upload starts, not
    /// at construction.
    /// </remarks>
    public int LoadedLatencyIntervalMilliseconds { get; init; } = 250;
}

