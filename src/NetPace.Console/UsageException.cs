namespace NetPace.Console;

/// <summary>
/// A refusal the command line alone decides, raised where the check cannot be a parser validator.
/// </summary>
/// <remarks>
/// The type is what tells a usage error apart from an operational fault once both have become an
/// exception, so the top-level handler can exit <see cref="ExitCode.UsageError"/> for the one and
/// <see cref="ExitCode.OperationalFault"/> for the other. Anything else that reaches the handler is
/// an operational fault: a fault is the safer reading of an exception nobody anticipated, since it
/// says NetPace could not do its job rather than blaming the command line.
/// <para>
/// Throw it only where the refusal would happen on any machine for the same arguments. A failure
/// that depends on the environment the run met, such as a file that could not be read, is an
/// operational fault however the path reached NetPace.
/// </para>
/// </remarks>
public sealed class UsageException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UsageException"/> class.
    /// </summary>
    public UsageException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UsageException"/> class.
    /// </summary>
    /// <param name="message">What the user is told, naming what to do about it.</param>
    public UsageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UsageException"/> class.
    /// </summary>
    /// <param name="message">What the user is told, naming what to do about it.</param>
    /// <param name="innerException">The refusal this one was raised for.</param>
    public UsageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
