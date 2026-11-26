namespace NetPace.Console;

/// <summary>
/// A refusal the command line alone decides, raised where the check cannot be a parser validator.
/// </summary>
/// <remarks>
/// The type is what tells a usage error apart from an operational fault once both have become an
/// exception, so the top-level handler can exit <see cref="ExitCode.UsageError"/> for the one and
/// <see cref="ExitCode.OperationalFault"/> for the other. Anything else that reaches the handler is
/// an operational fault. Why it is a type of its own rather than an existing framework exception:
/// <c>docs/change-intent-records/2026-11-26-each-outcome-exits-with-its-own-code.md</c>.
/// <para>
/// Throw it only where the refusal would happen on any machine for the same arguments. A failure
/// that depends on the environment the run met, such as a file that could not be read, is an
/// operational fault however the path reached NetPace.
/// </para>
/// </remarks>
public sealed class UsageException : Exception
{
    /// <summary>
    /// The message NetPace reports a refusal it has no detail of its own for, so a usage error
    /// always reaches the user as a sentence rather than as a type name.
    /// </summary>
    public UsageException()
        : base("The command line cannot be run as given.")
    {
    }

    /// <summary>
    /// Carries the message the user is told. This is the form every NetPace check throws.
    /// </summary>
    /// <param name="message">What the user is told, naming what to do about it.</param>
    public UsageException(string message) : base(message)
    {
    }

    /// <summary>
    /// Carries the message the user is told, and the refusal it was raised for.
    /// </summary>
    /// <param name="message">What the user is told, naming what to do about it.</param>
    /// <param name="innerException">The refusal this one was raised for.</param>
    public UsageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
