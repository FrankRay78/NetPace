using NetPace.Console.ConsoleWriters;
using Spectre.Console;
using Spectre.Console.Rendering;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests.ConsoleWriters;

public sealed class LiveSpeedColumnTests
{
    [Fact]
    public void Report_DataArrivingWhileATestRuns_ShowsTheAverageSpeedSinceTheTestBegan()
    {
        // SCENARIO: Live speed shown while a test runs

        // Given a download that reports more data with each update, the bytes catching the clock up
        // as it goes.
        var column = new LiveSpeedColumn(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto);
        var task = RunningTest("Downloading");

        SpeedTestProgress[] reports =
        [
            new() { PercentageComplete = 25, BytesProcessed = 100, ElapsedMilliseconds = 250 },
            new() { PercentageComplete = 50, BytesProcessed = 350, ElapsedMilliseconds = 500 },
            new() { PercentageComplete = 75, BytesProcessed = 650, ElapsedMilliseconds = 750 },
            new() { PercentageComplete = 100, BytesProcessed = 1000, ElapsedMilliseconds = 1000 },
        ];

        // When
        var figures = new List<string>();

        foreach (var report in reports)
        {
            column.Report(task, report);
            figures.Add(FigureShown(column, task));
        }

        // Then each figure is the average speed since the test began, so it changes as more data arrives.
        Assert.Equal(["3.2 Kbps", "5.6 Kbps", "6.93 Kbps", "8 Kbps"], figures);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(100, 0)]
    public void Report_BeforeAUsableMeasurementArrives_ShowsNothing(long bytesProcessed, long elapsedMilliseconds)
    {
        // Given a test that has started but has either moved no bytes or had no time to move them
        // in — a sub-millisecond first report reads as the latter.
        var column = new LiveSpeedColumn(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto);
        var task = RunningTest("Downloading");

        // When
        column.Report(task, new SpeedTestProgress { PercentageComplete = 0, BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds });

        // Then nothing is shown beside the bar: no placeholder, and no figure computed from nothing.
        Assert.Equal(string.Empty, FigureShown(column, task));
    }

    [Fact]
    public void Settle_WhenATestCompletes_ShowsTheSpeedTheResultReports()
    {
        // Given a last progress report that lags the result the test went on to return.
        var column = new LiveSpeedColumn(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto);
        var task = RunningTest("Downloading");
        column.Report(task, new SpeedTestProgress { PercentageComplete = 100, BytesProcessed = 900, ElapsedMilliseconds = 1000 });

        // When
        column.Settle(task, new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsSucceeded = 150, RequestsFailed = 0 });

        // Then the figure ends on the speed the result reports.
        Assert.Equal("8 Kbps", FigureShown(column, task));
    }

    [Theory]
    [InlineData(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC, SpeedScale.Mega, "119.21 MiBps")]
    [InlineData(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Base, "1000000000 bps")]
    [InlineData(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto, "1 Gbps")]
    public void Report_UnderEveryUnitOption_FormatsTheFigureThatWay(SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale, string expected)
    {
        // Given a gigabit download part-way through.
        var column = new LiveSpeedColumn(unit, unitSystem, scale);
        var task = RunningTest("Downloading");

        // When
        column.Report(task, new SpeedTestProgress { PercentageComplete = 50, BytesProcessed = 125_000_000, ElapsedMilliseconds = 1000 });

        // Then
        Assert.Equal(expected, FigureShown(column, task));
    }

    private static ProgressTask RunningTest(string description) =>
        new(id: 0, description, maxValue: 100, autoStart: true, TimeProvider.System);

    /// <summary>
    /// What the column puts beside the task's progress bar, as rendered to a console.
    /// </summary>
    private static string FigureShown(LiveSpeedColumn column, ProgressTask task)
    {
        using var console = new TestConsole().Width(CommandLineTestHost.InteractiveTerminalWidth);

        console.Write(column.Render(RenderOptions.Create(console, console.Profile.Capabilities), task, TimeSpan.Zero));

        return console.Output.Trim();
    }
}
