namespace NetPace.Core;

/// <summary>
/// What one request of a speed test did: when it started, where it went, how it ended, and — where
/// it did not succeed — why.
/// </summary>
/// <remarks>
/// Reported on its own <see cref="IProgress{T}"/> channel, separate from the cumulative progress
/// channel, so a consumer that renders no progress display can still receive per-request detail.
/// The failure cause travels as text rather than as an <see cref="Exception"/>, for a triager to
/// read.
/// </remarks>
public sealed record RequestDiagnostic
{
    /// <summary>
    /// The <see cref="FailureReason"/> recorded for a request that was still in flight when the
    /// caller asked the measurement to stop and report what it had.
    /// </summary>
    /// <remarks>
    /// Deliberately generic: the library knows a caller asked it to stop, not what that caller's
    /// own stop was for. A consumer that bounds a run by a clock, a data allowance or a user's
    /// keystroke replaces this with wording that names its own reason before showing the record to
    /// anyone. It is a reason rather than a failure - the request counts as neither succeeded nor
    /// failed, and its <see cref="Outcome"/> is <see cref="RequestOutcome.Cancelled"/>.
    /// </remarks>
    public const string StoppedByCaller = "stopped by caller";

    /// <summary>
    /// Gets the time the request started, taken from the provider's clock at the moment the
    /// request was issued rather than when the consumer received this record.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets what the request was for. Which kinds a test is measuring depends on the test, so this
    /// alone does not separate a measurement from the requests made alongside it; see
    /// <see cref="RequestKind"/>.
    /// </summary>
    public required RequestKind Kind { get; init; }

    /// <summary>
    /// Gets the request's position within its test, numbered from one in the order requests were
    /// issued. Records arrive in completion order, so a parallel test reports these out of order.
    /// </summary>
    /// <remarks>
    /// The latency probes sent during a download or an upload are numbered among themselves, so the
    /// transfer's own requests keep an unbroken run however many probes went out beside them — and a
    /// probe and a transfer within one test can therefore carry the same number. It is
    /// <see cref="Kind"/> and this together that identify a request within a test.
    /// </remarks>
    public required int Sequence { get; init; }

    /// <summary>
    /// Gets the address the request targeted.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets how the request ended.
    /// </summary>
    public required RequestOutcome Outcome { get; init; }

    /// <summary>
    /// Gets the number of bytes this request moved, whatever its outcome: a request the server
    /// cut off partway, and one dropped because the byte budget was reached, each report what they
    /// had moved by then. Zero means nothing arrived, not that the request did not finish.
    /// </summary>
    public required long BytesProcessed { get; init; }

    /// <summary>
    /// Gets how long the request took, in milliseconds.
    /// </summary>
    public required long ElapsedMilliseconds { get; init; }

    /// <summary>
    /// Gets why the request did not succeed, or <see langword="null"/> when it did. Where an
    /// exception caused the failure this is its chain's messages joined with <c>" &lt;- "</c>,
    /// outermost first; otherwise a short description of what happened.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Gets the type name of the outermost exception behind <see cref="FailureReason"/>, or
    /// <see langword="null"/> where no exception was involved.
    /// </summary>
    public string? FailureType { get; init; }
}
