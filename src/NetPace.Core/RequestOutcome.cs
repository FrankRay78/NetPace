namespace NetPace.Core;

/// <summary>
/// How a single speed test request ended.
/// </summary>
public enum RequestOutcome
{
    /// <summary>
    /// The request completed and its bytes counted towards the measurement.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The request failed — a transport error, a TLS failure, a timeout, or a non-success status.
    /// </summary>
    Failed,

    /// <summary>
    /// The request was abandoned rather than failing on its own merits: the total-byte budget was
    /// reached, the server-selection ceiling expired, the caller cancelled, or - for a latency probe -
    /// the transfer it was probing beside ended.
    /// </summary>
    Cancelled
}
