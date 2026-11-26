namespace NetPace.Console;

/// <summary>
/// The codes NetPace exits with, one per kind of outcome, so a script or a monitoring check can
/// tell what went wrong from the code alone.
/// </summary>
/// <remarks>
/// A network outcome is data rather than a failure, so it leaves the code at
/// <see cref="Success"/> unless the user opted in with <c>--fail-on</c> or a target. Where more
/// than one of the rest applies to a run, the most serious is reported:
/// <see cref="OperationalFault"/>, then <see cref="FailOnTriggered"/>, then
/// <see cref="MissedTarget"/>. A <see cref="UsageError"/> never runs a test, so it is never
/// combined with the others.
/// <para>
/// These are an interface contract: a script reads them, so a code's meaning cannot change
/// without breaking it.
/// </para>
/// </remarks>
internal static class ExitCode
{
    /// <summary>
    /// NetPace ran, and met every opt-in condition that was asked for.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// The command line was usable, but NetPace could not do its job: the <c>--file</c> output
    /// could not be written, or the <c>--server-file</c> list could not be read.
    /// </summary>
    public const int OperationalFault = 1;

    /// <summary>
    /// The command line itself cannot be run, decided before any test. The same arguments would be
    /// refused on any machine.
    /// </summary>
    public const int UsageError = 2;

    /// <summary>
    /// A <c>--min-download</c>, <c>--min-upload</c> or <c>--max-latency</c> target was missed.
    /// </summary>
    public const int MissedTarget = 3;

    /// <summary>
    /// <c>--fail-on</c> was triggered by a failed measurement, or by one that never ran.
    /// </summary>
    public const int FailOnTriggered = 4;
}
