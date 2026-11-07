namespace NetPace.Console.Commands;

/// <summary>
/// The split every command's exit code follows: whether a failure reflects NetPace's own health
/// rather than a network condition.
/// </summary>
/// <remarks>
/// Network conditions — an outage, a server that never answered, nothing discovered at all — are
/// data, so they are reported and leave the exit code at <c>0</c>. An operational fault is
/// NetPace failing at its own job, and must exit non-zero. One definition, shared, because a
/// command that split them differently would make the documented exit-code rule depend on which
/// command the user ran.
/// </remarks>
internal static class OperationalFault
{
    /// <summary>
    /// Whether <paramref name="exception"/> is an operational fault — for example, the
    /// <c>--file</c> target becoming unwritable mid-run.
    /// </summary>
    /// <remarks>
    /// <see cref="HttpIOException"/> is excluded because it derives from <see cref="IOException"/>:
    /// a reset connection is a network condition, not ours.
    /// </remarks>
    public static bool Matches(Exception exception) =>
        exception is (IOException and not HttpIOException) or UnauthorizedAccessException;
}
