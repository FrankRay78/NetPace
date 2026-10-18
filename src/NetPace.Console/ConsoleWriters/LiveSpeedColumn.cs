using System.Collections.Concurrent;
using NetPace.Core;
using Spectre.Console.Rendering;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The live speed shown beside a running test's progress bar: the average speed since that test
/// began, written in the same unit options the result line uses.
/// </summary>
/// <remarks>
/// An average over the whole test rather than the speed of the moment, so the figure climbs and
/// steadies as the test runs and lands on the speed the result line goes on to report. One column
/// instance serves every bar on screen, holding each test's figure against the bar it belongs to.
/// </remarks>
internal sealed class LiveSpeedColumn(SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale) : ProgressColumn
{
    // Written from the thread a measurement reports on and read by Spectre's own refresh thread,
    // so the figures cannot live in a plain Dictionary.
    private readonly ConcurrentDictionary<int, string> _figures = new();

    /// <inheritdoc/>
    protected override bool NoWrap => true;

    /// <summary>
    /// Shows the average speed a progress report stands for.
    /// </summary>
    /// <remarks>
    /// Nothing is shown until a report carries both bytes and elapsed time, so a bar shows no
    /// placeholder and no figure computed from nothing. A provider that reports a percentage and
    /// nothing else therefore shows no figure at all.
    /// </remarks>
    internal void Report(ProgressTask task, SpeedTestProgress progress)
    {
        if (progress.BytesProcessed <= 0 || progress.ElapsedMilliseconds <= 0)
        {
            return;
        }

        Show(task, progress.GetSpeedString(unit, unitSystem, scale));
    }

    /// <summary>
    /// Shows the speed a finished test's result reports, so the figure the user watched ends on the
    /// figure they are given.
    /// </summary>
    /// <remarks>
    /// The figure carries the speed alone. Where requests failed the result line adds a note saying
    /// how many, and that note stays on the result line rather than being repeated beside the bar.
    /// </remarks>
    internal void Settle(ProgressTask task, SpeedTestResult result)
    {
        Show(task, result.GetSpeedString(unit, unitSystem, scale));
    }

    /// <inheritdoc/>
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new Text(_figures.GetValueOrDefault(task.Id, string.Empty));
    }

    private void Show(ProgressTask task, string figure) => _figures[task.Id] = figure;
}
