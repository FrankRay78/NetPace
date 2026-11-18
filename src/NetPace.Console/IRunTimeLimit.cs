namespace NetPace.Console;

/// <summary>
/// Bounds how long one speed test run may take.
/// </summary>
/// <remarks>
/// A seam rather than a bare timer, so a test can decide when a run is cut short instead of
/// waiting out a duration: a suite that had to sit through the limit to watch it fire would be
/// neither fast nor repeatable.
/// </remarks>
public interface IRunTimeLimit
{
    /// <summary>
    /// Starts the limit for one run, returning the source that fires when it is reached. The
    /// caller owns it and disposes it when the run ends, so the next run starts a limit of its own.
    /// </summary>
    /// <param name="limit">How long the run may take. Always a positive duration.</param>
    CancellationTokenSource Start(TimeSpan limit);
}

/// <summary>
/// The run time limit as a clock keeps it.
/// </summary>
public sealed class RunTimeLimit : IRunTimeLimit
{
    /// <summary>
    /// A bound under the longest limit a timer accepts. A limit beyond it is kept at it, which no real run comes
    /// near: this exists so that an absurd duration on the command line is a run nobody waits out
    /// rather than an exception out of the timer.
    /// </summary>
    private static readonly TimeSpan LongestLimit = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <inheritdoc/>
    public CancellationTokenSource Start(TimeSpan limit) =>
        new(limit < LongestLimit ? limit : LongestLimit);
}
