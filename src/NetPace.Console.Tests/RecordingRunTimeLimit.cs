namespace NetPace.Console.Tests;

/// <summary>
/// An <see cref="IRunTimeLimit"/> whose limit is reached when a test says so rather than when a
/// clock says so, and which records the limit every run was given.
/// </summary>
/// <remarks>
/// A real limit would have the suite wait out a duration to watch it fire. Reaching it on demand
/// puts the moment a run is cut short under the test's control.
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

    /// <summary>
    /// Whether a run's limit has been started and not yet disposed, so a test can tell whether
    /// something happened while a limit was running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <inheritdoc/>
    public CancellationTokenSource Start(TimeSpan limit)
    {
        started.Add(limit);
        IsRunning = true;
        Current = new Source(() => IsRunning = false);
        return Current;
    }

    /// <summary>
    /// Reaches the limit of the run in progress, as a clock running out would.
    /// </summary>
    public void Reach() =>
        (Current ?? throw new InvalidOperationException("No run has started, so there is no limit to reach.")).Cancel();

    /// <summary>A limit that says when the run that owns it has disposed it.</summary>
    private sealed class Source(Action disposed) : CancellationTokenSource
    {
        protected override void Dispose(bool disposing)
        {
            disposed();
            base.Dispose(disposing);
        }
    }
}
