namespace NetPace.Console;

/// <summary>
/// Interface for whether a live display — the spinner and the table a run draws while it
/// measures — redraws itself as it goes.
/// </summary>
/// <remarks>
/// Injected because a screen whose frame count varies with the machine cannot be snapshotted. See
/// CIR <c>docs/change-intent-records/2026-11-03-live-displays-redraw-on-an-injected-switch.md</c>.
/// </remarks>
public interface ILiveDisplayRefresh
{
    /// <summary>
    /// Whether the display redraws on Spectre.Console's own refresh interval, so a spinner turns
    /// and the table's figures move while a measurement is still in flight.
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
/// A live display that does not redraw as it goes: Spectre.Console draws it once as the display
/// closes, so a capture holds one frame per display — the final screen — however long the
/// measurement took.
/// </summary>
/// <remarks>
/// Registered only by the console test host, so a captured screen is a function of what was
/// measured. Production keeps redrawing, or anything meant to be seen mid-run would be lost.
/// </remarks>
public sealed class NoRefreshStub : ILiveDisplayRefresh
{
    /// <inheritdoc/>
    public bool AutoRefresh => false;
}
