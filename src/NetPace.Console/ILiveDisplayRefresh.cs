namespace NetPace.Console;

/// <summary>
/// Whether a live display — the spinner and progress bars a run draws while it measures — redraws
/// itself on its own wall-clock interval.
/// </summary>
/// <remarks>
/// This is the clock the display's frames come from, and it is injected for the same reason the
/// <see cref="IClock"/> is: how many frames a run produces otherwise depends on how long the
/// measurement took, and a captured screen that holds a different number of frames on a slow
/// machine cannot be snapshotted. See the change-intent record
/// <c>docs/change-intent-records/2026-11-03-live-displays-redraw-on-an-injected-switch.md</c>.
/// </remarks>
public interface ILiveDisplayRefresh
{
    /// <summary>
    /// Whether the display redraws on Spectre.Console's refresh interval, so a spinner turns and a
    /// bar advances while a measurement is still in flight.
    /// </summary>
    bool AutoRefresh { get; }
}

/// <summary>
/// A live display that redraws as it goes, which is what a person watching a run expects.
/// </summary>
public sealed class LiveDisplayRefresh : ILiveDisplayRefresh
{
    /// <inheritdoc/>
    public bool AutoRefresh => true;
}

/// <summary>
/// A live display that redraws only where the run itself asks it to, so a capture holds one frame
/// per display — the final screen — however long the measurement took.
/// </summary>
/// <remarks>
/// Useful for testing: it makes the captured screen a function of what was measured rather than of
/// how fast the machine ran.
/// </remarks>
public sealed class NoRefreshStub : ILiveDisplayRefresh
{
    /// <inheritdoc/>
    public bool AutoRefresh => false;
}
