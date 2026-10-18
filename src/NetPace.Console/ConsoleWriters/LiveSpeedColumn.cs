using NetPace.Core;
using Spectre.Console.Rendering;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The live speed shown beside a running test's progress bar: the average speed since that test
/// began, formatted exactly as the result line formats it.
/// </summary>
/// <remarks>
/// An average over the whole test rather than the speed of the moment, so the figure climbs and
/// steadies as the test runs and lands on the figure the result line goes on to report. One column
/// instance serves every bar on screen, holding each test's figure against the bar it belongs to.
/// </remarks>
internal sealed class LiveSpeedColumn(SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale) : ProgressColumn
{
    private readonly Dictionary<int, string> figures = [];

    /// <inheritdoc/>
    protected override bool NoWrap => true;

    /// <summary>
    /// Shows the average speed a progress report stands for.
    /// </summary>
    /// <remarks>
    /// Nothing is shown until data has actually arrived, so a test that has not moved a byte yet
    /// carries no placeholder beside its bar and never a zero speed.
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
    internal void Settle(ProgressTask task, SpeedTestResult result)
    {
        Show(task, result.GetSpeedString(unit, unitSystem, scale));
    }

    /// <inheritdoc/>
    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new Text(figures.TryGetValue(task.Id, out var figure) ? figure : string.Empty);
    }

    private void Show(ProgressTask task, string figure)
    {
        ArgumentNullException.ThrowIfNull(task);

        figures[task.Id] = figure;
    }
}
