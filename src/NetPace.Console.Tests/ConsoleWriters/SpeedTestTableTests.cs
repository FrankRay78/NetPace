using NetPace.Console.ConsoleWriters;
using NetPace.Console.Theming;
using NetPace.Core;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests.ConsoleWriters;

/// <summary>
/// The live table a speed test draws at a terminal: one row per measurement, with the figures
/// arriving beside the test they belong to.
/// </summary>
/// <remarks>
/// Rendered straight to a test console rather than through a run, because the figures a row shows
/// mid-measurement are the point: a run's captured screen holds only the frame drawn as the
/// display closed, by which time every measurement has finished.
/// </remarks>
public sealed class SpeedTestTableTests
{
    private static SpeedTestTable Table(bool showLatency = true) =>
        new(Theme.Plain, SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto, showLatency, animated: false);

    /// <summary>
    /// What the table looks like on a wide, Unicode-capable screen with nothing styled, so an
    /// assertion reads the figures rather than the escape sequences around them.
    /// </summary>
    private static string Render(SpeedTestTable table)
    {
        var console = new TestConsole().Width(120);
        console.Write(table);
        return console.Output;
    }

    private static LatencyTestResult Latency(long latencyMilliseconds, long jitterMilliseconds) =>
        new()
        {
            Server = new Server { Url = "http://test1.com", Sponsor = "Sponsor" },
            LatencyMilliseconds = latencyMilliseconds,
            MinimumLatencyMilliseconds = null,
            MaximumLatencyMilliseconds = null,
            JitterMilliseconds = jitterMilliseconds
        };

    /// <summary>
    /// A transfer result. A transfer whose every request failed moved no bytes, which is what makes
    /// its speed a zero rather than a figure.
    /// </summary>
    private static SpeedTestResult Transfer(int succeeded, int failed, LatencyTestResult? latency) =>
        new()
        {
            BytesProcessed = succeeded == 0 ? 0 : 1_000_000,
            ElapsedMilliseconds = 1000,
            RequestsSucceeded = succeeded,
            RequestsFailed = failed,
            Latency = latency
        };

    [Fact]
    public void AReportedFigure_IsShownBeforeTheMeasurementEnds()
    {
        // SCENARIO: Figures are live while a test runs

        // Given a table with a transfer row under way.
        var table = Table();
        var row = table.AddRow("Downloading");

        // When the transfer reports progress whose probes have answered once.
        row.Report(new SpeedTestProgress
        {
            PercentageComplete = 30,
            BytesProcessed = 300_000,
            ElapsedMilliseconds = 1000,
            LatencyMilliseconds = 38,
            JitterMilliseconds = null
        });

        // Then the row already shows the running latency, and the jitter cell stays blank until a
        // second probe has answered.
        var running = Render(table);
        Assert.Contains("30%", running);
        Assert.Contains("38 ms", running);

        // And when a later report carries both.
        row.Report(new SpeedTestProgress
        {
            PercentageComplete = 70,
            BytesProcessed = 700_000,
            ElapsedMilliseconds = 1000,
            LatencyMilliseconds = 42,
            JitterMilliseconds = 3
        });

        // Then the row shows the newer figures, so what the user watches moves with the
        // measurement.
        var later = Render(table);
        Assert.Contains("42 ms", later);
        Assert.Contains("3 ms", later);
        Assert.DoesNotContain("38 ms", later);
    }

    [Fact]
    public void AReportedFigure_EndsOnTheFigureTheResultCarries()
    {
        // Given a transfer row that reported a running latency.
        var table = Table();
        var row = table.AddRow("Downloading");
        row.Report(new SpeedTestProgress { PercentageComplete = 70, BytesProcessed = 700_000, ElapsedMilliseconds = 1000, LatencyMilliseconds = 42, JitterMilliseconds = 3 });

        // When the measurement finishes with figures of its own.
        row.Settle(Transfer(succeeded: 10, failed: 0, latency: Latency(60, 4)));

        // Then the row ends on the result's figures, not on the last one it was watching.
        var settled = Render(table);
        Assert.Contains("60 ms", settled);
        Assert.Contains("4 ms", settled);
        Assert.DoesNotContain("42 ms", settled);
        Assert.Contains("100%", settled);
    }

    [Fact]
    public void ATransferThatMeasuredNoLatency_LeavesItsCellsBlankAndKeepsItsSpeed()
    {
        // SCENARIO: Unmeasured figures are blank

        // Given a transfer whose result says latency under load was not measured.
        var table = Table();
        var row = table.AddRow("Uploading");

        // When it settles.
        row.Settle(Transfer(succeeded: 10, failed: 0, latency: null));

        // Then the speed is shown and nothing stands in for the figures that were not measured.
        var settled = Render(table);
        Assert.Contains("8 Mbps", settled);
        Assert.DoesNotContain("ms", settled);
        Assert.DoesNotContain("0 ms", settled);
    }

    [Fact]
    public void ATransferWithSomeFailedRequests_KeepsItsFiguresAndCountsTheFailures()
    {
        // SCENARIO: A partial failure keeps its figures

        // Given a transfer in which one of ten requests failed.
        var table = Table();
        var row = table.AddRow("Downloading");

        // When it settles.
        row.Settle(Transfer(succeeded: 9, failed: 1, latency: Latency(60, 4)));

        // Then its figures stand, the count says how many failed, and the symbol marks it as a
        // partial failure rather than a clean run.
        var settled = Render(table);
        Assert.Contains("8 Mbps", settled);
        Assert.Contains("60 ms", settled);
        Assert.Contains("4 ms", settled);
        Assert.Contains("1 of 10", settled);
        Assert.Contains("!", settled);
    }

    [Fact]
    public void ATransferWhoseEveryRequestFailed_ShowsAZeroSpeedAndNoLatency()
    {
        // SCENARIO: A total failure shows no latency

        // Given a transfer in which every request failed, so no probe saw a loaded line.
        var table = Table();
        var row = table.AddRow("Uploading");

        // When it settles.
        row.Settle(Transfer(succeeded: 0, failed: 30, latency: null));

        // Then the speed reads zero, the latency and jitter cells are blank, and the count says
        // every request failed.
        var settled = Render(table);
        Assert.Contains("0 bps", settled);
        Assert.DoesNotContain("ms", settled);
        Assert.Contains("30 of 30", settled);
    }

    [Fact]
    public void TheFailedColumn_AppearsOnlyOnceATransferHasFailed()
    {
        // Given a table whose transfers have all succeeded.
        var table = Table();
        var clean = table.AddRow("Downloading");
        var failing = table.AddRow("Uploading");
        clean.Settle(Transfer(succeeded: 10, failed: 0, latency: Latency(60, 4)));

        // Then nothing names a column of failures.
        Assert.DoesNotContain("Failed", Render(table));

        // When one of them finishes with failures.
        failing.Settle(Transfer(succeeded: 9, failed: 1, latency: Latency(60, 4)));

        // Then the column appears, and the row with no failures leaves its cell empty.
        var withFailures = Render(table);
        Assert.Contains("Failed", withFailures);
        Assert.Contains("1 of 10", withFailures);
    }

    [Fact]
    public void ATableWithoutLatency_NamesNeitherFigureInItsHeadings()
    {
        // Given a table built for a run that measures no latency.
        var table = Table(showLatency: false);
        var row = table.AddRow("Downloading");
        row.Settle(Transfer(succeeded: 10, failed: 0, latency: null));

        // Then neither column is drawn at all.
        var rendered = Render(table);
        Assert.DoesNotContain("Latency", rendered);
        Assert.DoesNotContain("Jitter", rendered);
        Assert.Contains("Speed", rendered);
    }

    [Fact]
    public void AMeasurementThatFailed_MarksItsRowAndKeepsTheOthers()
    {
        // Given a latency row that was running when its measurement threw.
        var table = Table();
        var pinging = table.AddRow("Pinging");

        // When it is marked as failed.
        pinging.Fail();

        // Then the row says so rather than disappearing.
        var rendered = Render(table);
        Assert.Contains("Pinging", rendered);
        Assert.Contains("✗", rendered);
    }

    [Fact]
    public void ALatencyRow_ShowsItsFiguresAndNoSpeed()
    {
        // Given a latency row that has measured.
        var table = Table();
        var pinging = table.AddRow("Pinging");

        // When it settles.
        pinging.Settle(Latency(3, 1));

        // Then the figures are on the row and nothing stands in for a speed it never measured.
        var rendered = Render(table);
        Assert.Contains("3 ms", rendered);
        Assert.Contains("1 ms", rendered);
        Assert.DoesNotContain("bps", rendered);
    }

    [Fact]
    public void ALatencyRow_ShowsTheRunningFiguresItIsReported()
    {
        // Given a latency row under way.
        var table = Table();
        var pinging = table.AddRow("Pinging");

        // When a probe answers.
        pinging.Report(new LatencyTestProgress { PercentageComplete = 25, LatencyMilliseconds = 3, JitterMilliseconds = null });

        // Then the figure is already on the row, with the jitter cell still blank.
        var rendered = Render(table);
        Assert.Contains("3 ms", rendered);
        Assert.Contains("25%", rendered);
    }

    [Fact]
    public void AGrowingFigure_IsShownWholeAndTheColumnsStillLineUp()
    {
        // SCENARIO: Columns grow to fit

        // Given a table showing a small figure.
        var table = Table();
        var row = table.AddRow("Downloading");
        row.Report(new SpeedTestProgress { PercentageComplete = 10, BytesProcessed = 1000, ElapsedMilliseconds = 1000, LatencyMilliseconds = 3, JitterMilliseconds = 1 });

        // When a much larger figure arrives, and the Failed column appears with it.
        row.Settle(Transfer(succeeded: 998, failed: 2, latency: Latency(1234, 567)));

        // Then every value is shown whole rather than cut off.
        var rendered = Render(table);
        Assert.Contains("1234 ms", rendered);
        Assert.Contains("567 ms", rendered);
        Assert.Contains("2 of 1000", rendered);

        // And the headings still sit above their own values: one line each, so the columns the
        // header row names are the columns the value row fills.
        var lines = rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }
}
