namespace NetPace.Console.Commands;

/// <summary>
/// The split every command's exit code follows: whether a failure reflects NetPace's own health
/// rather than a network condition.
/// </summary>
/// <remarks>
/// Network conditions — an outage, a server that never answered, nothing discovered at all — are
/// data, so they are reported and leave the exit code at <c>0</c>. An operational fault is
/// NetPace failing at its own job, and must exit non-zero. One definition, shared, because a
/// command that recognised operational faults differently would make the documented exit-code
/// rule depend on which command the user ran. It is one component of that rule rather than the
/// whole of it: a command may exit non-zero for a usage error too, as <c>servers</c> does.
/// </remarks>
internal static class OperationalFault
{
    /// <summary>
    /// Whether <paramref name="exception"/> is an operational fault — for example, the
    /// <c>--file</c> target becoming unwritable mid-run.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpIOException"/> is excluded despite deriving from <see cref="IOException"/>:
    /// a reset connection is a network condition, not ours.
    /// </remarks>
    public static bool Matches(Exception exception) =>
        exception is (IOException and not HttpIOException) or UnauthorizedAccessException;
}
