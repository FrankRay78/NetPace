namespace NetPace.Core;

/// <summary>
/// The latency test result for a specific server.
/// </summary>
/// <remarks>
/// Every property is <c>required</c>, so no value this record reports can be silently defaulted —
/// each must be stated at construction. It does not follow that a stated value was measured: a
/// caller that skipped the latency measurement states <see cref="LatencyMilliseconds"/> as zero
/// explicitly, and this type does not distinguish that from a measured zero.
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
}
