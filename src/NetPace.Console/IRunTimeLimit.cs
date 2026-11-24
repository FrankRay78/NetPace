namespace NetPace.Console;

/// <summary>
/// Bounds how long one speed test run may take.
/// </summary>
/// <remarks>
/// A seam rather than a bare timer, so a test decides when a run is cut short: a suite that sat
/// through the limit to watch it fire would be neither fast nor repeatable.
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
    /// <inheritdoc/>
    public CancellationTokenSource Start(TimeSpan limit) => new(limit);
}
