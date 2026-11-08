namespace NetPace.Core;

/// <summary>
/// Thrown when no speed test server could be found or reached, so there is nothing to measure
/// against.
/// </summary>
/// <remarks>
/// This is a network outcome rather than a fault: every server screening was given failed to
/// answer. A consumer that wants to retry later, fall back to a server of its own or show a
/// friendly message catches this type on its own, without a catch-all that would also swallow a
/// genuine defect. Discovery finding nothing is the caller's own check, because
/// <see cref="ISpeedTestService.GetServersAsync"/> reports it by returning an empty array rather
/// than by throwing.
/// </remarks>
public sealed class NoServersAvailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class carrying
    /// the message NetPace reports this condition with — the one its own console prints after the
    /// <c>Error:</c> prefix, so a consumer is free to show it as it stands. This is the form every
    /// NetPace component throws.
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
