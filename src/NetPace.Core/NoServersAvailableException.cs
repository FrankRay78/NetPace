namespace NetPace.Core;

/// <summary>
/// Thrown when no speed test server could be found or reached, so there is nothing to measure
/// against.
/// </summary>
/// <remarks>
/// This is a network outcome rather than a fault: discovery returned nothing, or every server it
/// returned failed to answer. A consumer that wants to retry later, fall back to a server of its
/// own or show a friendly message catches this type on its own, without a catch-all that would
/// also swallow a genuine defect. The message is what NetPace's own console prints, so a consumer
/// is free to show it as it stands.
/// </remarks>
public sealed class NoServersAvailableException : Exception
{
    /// <summary>
    /// The message every NetPace component reports this condition with.
    /// </summary>
    internal const string DefaultMessage = "No servers available";

    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class with the
    /// message NetPace reports this condition with.
    /// </summary>
    public NoServersAvailableException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class with a
    /// message of your own.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public NoServersAvailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NoServersAvailableException"/> class with a
    /// message of your own and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this one.</param>
    public NoServersAvailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
