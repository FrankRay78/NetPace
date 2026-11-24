namespace NetPace.Console.Commands;

/// <summary>
/// The split every command's exit code follows: whether a failure reflects NetPace's own health
/// rather than a network condition.
/// </summary>
/// <remarks>
/// A network condition is data, so it leaves the exit code at <c>0</c> unless the user opted in
/// with <c>--fail-on</c> or a target; an operational fault is NetPace failing at its own job.
/// <para>
/// One shared definition, or the documented exit-code rule would depend on which command ran. One
/// component of that rule, not the whole: a usage error exits non-zero too.
/// </para>
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
