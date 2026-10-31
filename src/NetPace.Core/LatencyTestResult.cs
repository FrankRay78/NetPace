namespace NetPace.Core;

/// <summary>
/// The latency test result for a specific server.
/// </summary>
/// <remarks>
/// <para>
/// Every property is <c>required</c>, so no value this record reports can be silently defaulted —
/// each must be stated at construction. It does not follow that a stated value was measured: a
/// caller that skipped the latency measurement states <see cref="LatencyMilliseconds"/> as zero
/// explicitly, and this type does not distinguish that from a measured zero.
/// </para>
/// <para>
/// The same applies to <see cref="JitterMilliseconds"/>, and a measurement of fewer than two
/// successful probes states it as zero: unevenness is a change from one round trip to the next, so
/// a single probe has nothing to compare and reports no unevenness rather than no figure.
/// </para>
/// </remarks>
public sealed record LatencyTestResult
{
    /// <summary>
    /// Gets the server that was tested.
    /// </summary>
    public required IServer Server { get; init; }

    /// <summary>
    /// Gets the measured latency to the server, in milliseconds.
    /// </summary>
    public required long LatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets the measured jitter to the server, in milliseconds: how uneven the round trips were,
    /// as the mean of the absolute differences between consecutive successful probe times.
    /// </summary>
    /// <remarks>
    /// Probes that all return in the same time report zero, as does a measurement of fewer than
    /// two successful probes, which has no consecutive pair to compare.
    /// </remarks>
    public required long JitterMilliseconds { get; init; }
}
