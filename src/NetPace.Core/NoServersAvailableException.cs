namespace NetPace.Core;

/// <summary>
/// Thrown when no speed test server could be found or reached, so there is nothing to measure
/// against.
/// </summary>
/// <remarks>
/// A network outcome rather than a fault: every server screening was given failed to answer. Its
/// own type, so a consumer can retry, fall back or show a friendly message without a catch-all that
/// would also swallow a genuine defect. Discovery finding nothing is the caller's own check:
/// <see cref="ISpeedTestService.GetServersAsync"/> returns an empty array rather than throwing.
/// </remarks>
public sealed class NoServersAvailableException : Exception
{
    /// <summary>
    /// Initializes a new instance carrying the message NetPace itself reports this condition with,
    /// which a consumer is free to show as it stands. This is the form every NetPace component
    /// throws.
    /// </summary>
    public NoServersAvailableException()
        : base("No servers available")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class with a
    /// message of your own, which replaces the one NetPace reports.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NoServersAvailableException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class with a
    /// message of your own, which replaces the one NetPace reports, and the exception that caused
    /// it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this one.</param>
    public NoServersAvailableException(string? message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
