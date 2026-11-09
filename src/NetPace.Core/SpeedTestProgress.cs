namespace NetPace.Core;

/// <summary>
/// The progress update for an inflight speed test.
/// </summary>
public sealed record SpeedTestProgress
{
    /// <summary>
    /// Gets the percentage complete.
    /// </summary>
    public int PercentageComplete { get; init; }

    /// <summary>
    /// Gets the total number of bytes processed.
    /// </summary>
    public long BytesProcessed { get; init; }

    /// <summary>
    /// Gets the total elapsed time, in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Gets the latency measured so far under this transfer's own load — the running figure behind
    /// <see cref="SpeedTestResult.Latency"/> — or <see langword="null"/> where there is nothing yet
    /// to report.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mean of the probes that have answered since the transfer began, so it compares directly
    /// with an idle measurement and ends on the figure the result goes on to report.
    /// <see langword="null"/> says nothing measured yet, which is not the same as zero: the
    /// measurement is switched off, or no probe has answered.
    /// </para>
    /// <para>
    /// This figure and <see cref="JitterMilliseconds"/> describe the one server under test. Server
    /// screening reports its progress through this same record while it measures several
    /// candidates, and leaves both absent, because no single figure would say which candidate it
    /// belonged to.
    /// </para>
    /// </remarks>
    public long? LatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets how uneven the probes behind <see cref="LatencyMilliseconds"/> have been so far, in
    /// milliseconds, or <see langword="null"/> where there is nothing yet to report.
    /// </summary>
    /// <remarks>
    /// Absent on the same terms as <see cref="LatencyMilliseconds"/>, and for one further reason:
    /// unevenness is a change from one round trip to the next, so a single probe has no consecutive
    /// pair to compare and reports no figure rather than a zero.
    /// </remarks>
    public long? JitterMilliseconds { get; init; }
}
