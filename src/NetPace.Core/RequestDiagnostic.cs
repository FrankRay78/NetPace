namespace NetPace.Core;

/// <summary>
/// What one request of a speed test did: when it started, where it went, how it ended, and — where
/// it did not succeed — why.
/// </summary>
/// <remarks>
/// Reported on its own <see cref="IProgress{T}"/> channel, separate from the cumulative progress
/// channel, so a consumer that renders no progress display can still receive per-request detail.
/// The failure cause travels as text rather than as an <see cref="Exception"/>: a triager reads the
/// reason, and keeping exception objects off this surface keeps the channel cheap to consume.
/// </remarks>
public sealed record RequestDiagnostic
{
    /// <summary>
    /// Gets the time the request started, taken from the provider's clock at the moment the
    /// request was issued rather than when the consumer received this record.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Gets the request's position within its test, numbered from one in the order requests were
    /// issued. Records arrive in completion order, so a parallel test reports these out of order.
    /// </summary>
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
    /// Gets the number of bytes this request moved. Zero for a request whose transfer did not
    /// complete, even where some bytes had already arrived.
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
