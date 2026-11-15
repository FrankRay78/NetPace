namespace NetPace.Console.Tests;

/// <summary>
/// An <see cref="IRunTimeLimit"/> whose limit is reached when a test says so rather than when a
/// clock says so, and which records the limit every run was given.
/// </summary>
/// <remarks>
/// A real limit would have the suite wait out a duration to watch it fire, which is neither fast
/// nor repeatable. Reaching it on demand puts the moment a run is cut short under the test's
/// control, so what is asserted is what the run does about it.
/// </remarks>
public sealed class RecordingRunTimeLimit : IRunTimeLimit
{
    private readonly List<TimeSpan> started = [];

    /// <summary>
    /// The limit each run was started with, in the order the runs happened: one entry per speed
    /// test, so a repeated run shows whether every run was given the limit afresh.
    /// </summary>
    public IReadOnlyList<TimeSpan> Started => started;

    /// <summary>
    /// The limit the run in progress is under, or <see langword="null"/> before the first run
    /// starts.
    /// </summary>
    public CancellationTokenSource? Current { get; private set; }

    /// <inheritdoc/>
    public CancellationTokenSource Start(TimeSpan limit)
    {
        started.Add(limit);
        Current = new CancellationTokenSource();
        return Current;
    }

    /// <summary>
    /// Reaches the limit of the run in progress, as a clock running out would.
    /// </summary>
    public void Reach() =>
        (Current ?? throw new InvalidOperationException("No run has started, so there is no limit to reach.")).Cancel();
}
