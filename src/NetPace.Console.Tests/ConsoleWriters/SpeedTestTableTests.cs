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
/// Rendered straight to a test console, because the figures a row shows mid-measurement are the
/// point: a run's captured screen holds only the frame drawn as the display closed.
/// </remarks>
public sealed class SpeedTestTableTests
{
    private static SpeedTestTable Table(bool showLatency = true, SpeedUnitSystem unitSystem = SpeedUnitSystem.SI) =>
        new(Theme.Plain, SpeedUnit.BitsPerSecond, unitSystem, SpeedScale.Auto, showLatency, animated: false);

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
    /// A transfer result. One whose every request failed is given no bytes here, so its speed reads
    /// as a zero; a real one can carry what its requests moved before they failed.
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
    public void AnAnimatedSpinner_TurnsAsTheMeasurementReports()
    {
        // Given an animated table with a measurement under way, which is what a run at a real
        // terminal builds - every other test here holds the spinner still so a capture does not
        // depend on how much the run happened to report.
        var table = new SpeedTestTable(Theme.Plain, SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto, showLatency: true, animated: true);
        var row = table.AddRow("Downloading");

        row.Report(new SpeedTestProgress { PercentageComplete = 10, BytesProcessed = 100_000, ElapsedMilliseconds = 1000 });
        var first = Render(table);

        // When the measurement reports again.
        row.Report(new SpeedTestProgress { PercentageComplete = 20, BytesProcessed = 200_000, ElapsedMilliseconds = 1000 });

        // Then the mark against the row has moved on, so the screen reads as turning rather than
        // frozen while the transfer is still in flight.
        Assert.NotEqual(Mark(first), Mark(Render(table)));
    }

    [Fact]
    public void AConsoleThatCannotDrawUnicode_GetsPlainMarksInstead()
    {
        // Given a settled table and a console that reports no Unicode, as a legacy terminal does.
        var table = Table();
        table.AddRow("Downloading").Settle(Transfer(succeeded: 10, failed: 0, latency: null));
        table.AddRow("Uploading").Settle(Transfer(succeeded: 0, failed: 10, latency: null));

        var console = new TestConsole().Width(120);
        console.Profile.Capabilities.Unicode = false;

        // When it is drawn there.
        console.Write(table);

        // Then the outcome of each row is still readable, as the plain stand-ins rather than as a
        // glyph the console cannot draw.
        Assert.Contains("OK", console.Output);
        Assert.Contains("X", console.Output);
        Assert.DoesNotContain("\u2713", console.Output);
        Assert.DoesNotContain("\u2717", console.Output);
    }

    [Fact]
    public async Task OnAConsoleThatCannotDrawUnicode_TheColumnsHoldTheirPlaceAsARowSettles()
    {
        // Given a console that reports no Unicode, where a running row's spinner frame and a
        // settled row's mark are not the same width, and a table whose first frame is drawn with
        // every row still running, as a run's is.
        var table = Table();
        var pinging = table.AddRow("Pinging");
        table.AddRow("Downloading");
        var running = RenderWithoutUnicode(table);

        // When the first row settles.
        pinging.Settle(Latency(3, 1));
        var settled = RenderWithoutUnicode(table);

        // Then the headings sit at the same offsets in both frames, so no column moves sideways
        // when its row's mark changes from the spinner to the outcome.
        Assert.Equal(Headings(running), Headings(settled));
        await Verify(Frames(running, settled));
    }

    private static string RenderWithoutUnicode(SpeedTestTable table)
    {
        var console = new TestConsole().Width(120);
        console.Profile.Capabilities.Unicode = false;
        console.Write(table);
        return console.Output;
    }

    /// <summary>The first column of the first row of figures: the mark or spinner frame.</summary>
    private static string Mark(string rendered) =>
        rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries)[1].TrimStart().Split(' ')[0];

    /// <summary>
    /// The heading line of a frame. Every column's heading sits over its own values, so comparing
    /// this line between two frames is how a test reads whether a column has moved sideways.
    /// </summary>
    private static string Headings(string rendered) =>
        rendered.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('\r');

    /// <summary>
    /// Several frames of the same table, one after another with a blank line between, so a
    /// snapshot shows how the figures moved and in which column each one sat.
    /// </summary>
    private static string Frames(params string[] frames) => string.Join("\n", frames);

    [Fact]
    public async Task AReportedFigure_IsShownBeforeTheMeasurementEnds()
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

        var running = Render(table);

        // And when a later report carries both.
        row.Report(new SpeedTestProgress
        {
            PercentageComplete = 70,
            BytesProcessed = 700_000,
            ElapsedMilliseconds = 1000,
            LatencyMilliseconds = 42,
            JitterMilliseconds = 3
        });

        // Then the first frame already shows the running latency, with Jitter blank until a second
        // probe answers, and the later frame the newer figures. The snapshot ties each to its column.
        var later = Render(table);
        await Verify(Frames(running, later));
    }

    [Fact]
    public async Task AReportedFigure_EndsOnTheFigureTheResultCarries()
    {
        // Given a transfer row that reported a running latency.
        var table = Table();
        var row = table.AddRow("Downloading");
        row.Report(new SpeedTestProgress { PercentageComplete = 70, BytesProcessed = 700_000, ElapsedMilliseconds = 1000, LatencyMilliseconds = 42, JitterMilliseconds = 3 });

        // When the measurement finishes with figures of its own.
        row.Settle(Transfer(succeeded: 10, failed: 0, latency: Latency(60, 4)));

        // Then the row ends on the result's figures at 100%, not on the last one it was watching.
        await Verify(Render(table));
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
        Assert.Contains("8.00 Mbps", settled);
        Assert.DoesNotContain("ms", settled);
    }

    [Fact]
    public async Task ATransferWithSomeFailedRequests_KeepsItsFiguresAndCountsTheFailures()
    {
        // SCENARIO: A partial failure keeps its figures

        // Given a transfer in which one of ten requests failed.
        var table = Table();
        var row = table.AddRow("Downloading");

        // When it settles.
        row.Settle(Transfer(succeeded: 9, failed: 1, latency: Latency(60, 4)));

        // Then its figures stand, the count says how many failed, and the symbol marks it as a
        // partial failure rather than a clean run, each figure under its own heading.
        await Verify(Render(table));
    }

    [Fact]
    public async Task ATransferWhoseEveryRequestFailed_ShowsAZeroSpeedAndNoLatency()
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
        Assert.DoesNotContain("ms", settled);
        await Verify(settled);
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
    public async Task AMeasurementThatFailed_MarksItsRowAndKeepsTheOthers()
    {
        // Given a settled latency row, and a download that had reported a figure when it threw.
        var table = Table();
        table.AddRow("Pinging").Settle(Latency(3, 1));
        var downloading = table.AddRow("Downloading");
        downloading.Report(new SpeedTestProgress { PercentageComplete = 40, BytesProcessed = 400_000, ElapsedMilliseconds = 1000, LatencyMilliseconds = 38, JitterMilliseconds = 2 });

        // When the download is marked as failed.
        downloading.Fail();

        // Then its row says so rather than disappearing, keeps the figures it had already
        // reported, and the settled row beside it is left as it was.
        await Verify(Render(table));
    }

    [Fact]
    public async Task ALatencyRow_ShowsItsFiguresAndNoSpeed()
    {
        // Given a latency row that has measured.
        var table = Table();
        var pinging = table.AddRow("Pinging");

        // When it settles.
        pinging.Settle(Latency(3, 1));

        // Then the figures are on the row and nothing stands in for a speed it never measured.
        var rendered = Render(table);
        Assert.DoesNotContain("bps", rendered);
        await Verify(rendered);
    }

    [Fact]
    public async Task ALatencyRow_ShowsTheRunningFiguresItIsReported()
    {
        // Given a latency row under way.
        var table = Table();
        var pinging = table.AddRow("Pinging");

        // When a probe answers.
        pinging.Report(new LatencyTestProgress { PercentageComplete = 25, LatencyMilliseconds = 3, JitterMilliseconds = null });

        // Then the figure is already on the row under Latency, with the Jitter cell still blank.
        await Verify(Render(table));
    }

    [Fact]
    public async Task TheSpeedColumn_HoldsItsWidthAsTheSpeedChangesScale()
    {
        // SCENARIO: Speed column holds its width as the speed changes scale

        // Given a table whose first frame is drawn before any speed has been reported, as a run's
        // is while the idle latency is still being measured.
        var table = Table();
        table.AddRow("Pinging").Settle(Latency(3, 1));
        var row = table.AddRow("Downloading");
        List<string> frames = [Render(table)];

        // When the transfer reports speeds that pass from Kbps up to Mbps and back down again,
        // each a different number of characters wide.
        foreach ((long bytes, int percentage) in new[] { (1_000L, 10), (100_000L, 30), (1_000_000L, 55), (120_000L, 80), (1_000L, 95) })
        {
            row.Report(new SpeedTestProgress { PercentageComplete = percentage, BytesProcessed = bytes, ElapsedMilliseconds = 1000, LatencyMilliseconds = 38, JitterMilliseconds = 3 });
            frames.Add(Render(table));
        }

        // Then every frame's headings sit at the same offsets, so the Speed column and the columns
        // to its right stay where they are while the figure under them changes scale.
        Assert.All(frames, frame => Assert.Equal(Headings(frames[0]), Headings(frame)));
        await Verify(Frames([.. frames]));
    }

    [Fact]
    public async Task TheSpeedColumn_UnderIecAutoScaling_HoldsItsWidthThroughTheFourDigitBand()
    {
        // Given an IEC auto-scaled table, whose Kibps band runs up to 1023 and so writes a
        // four-figure whole number that SI auto-scaling, capped at 999, never produces.
        var table = Table(unitSystem: SpeedUnitSystem.IEC);
        table.AddRow("Pinging").Settle(Latency(3, 1));
        var row = table.AddRow("Downloading");
        List<string> frames = [Render(table)];

        // When the transfer climbs through that band and on into Mibps, then falls back.
        foreach ((long bytes, int percentage) in new[] { (1_024L, 10), (64_000L, 30), (129_280L, 55), (262_144L, 80), (64_000L, 95) })
        {
            row.Report(new SpeedTestProgress { PercentageComplete = percentage, BytesProcessed = bytes, ElapsedMilliseconds = 1000, LatencyMilliseconds = 38, JitterMilliseconds = 3 });
            frames.Add(Render(table));
        }

        // Then every frame's headings sit at the same offsets, including the one drawing the
        // four-figure reading: the width reserved for the widest IEC reading covers it, so a
        // column never has to grow mid-run on the unit system whose figures run widest.
        Assert.All(frames, frame => Assert.Equal(Headings(frames[0]), Headings(frame)));
        await Verify(Frames([.. frames]));
    }

    [Fact]
    public async Task AColumnWidenedByAnOutsizedValue_DoesNotNarrowAgain()
    {
        // SCENARIO: A column widened by an outsized value does not narrow again

        // Given a latency row that has reported a reading wider than the Latency column reserves.
        var table = Table();
        var row = table.AddRow("Pinging");
        row.Report(new LatencyTestProgress { PercentageComplete = 50, LatencyMilliseconds = 12345, JitterMilliseconds = 6 });
        var widened = Render(table);

        // When a later reading is narrower again.
        row.Report(new LatencyTestProgress { PercentageComplete = 75, LatencyMilliseconds = 42, JitterMilliseconds = 6 });

        // Then the column keeps the width the outsized reading gave it, so the run's remaining
        // frames do not shuffle back the other way.
        var narrower = Render(table);
        Assert.Equal(Headings(widened), Headings(narrower));
        await Verify(Frames(widened, narrower));
    }

    [Fact]
    public async Task AGrowingFigure_IsShownWholeAndTheColumnsStillLineUp()
    {
        // SCENARIO: Columns grow to fit

        // Given a table showing a small figure.
        var table = Table();
        var row = table.AddRow("Downloading");
        row.Report(new SpeedTestProgress { PercentageComplete = 10, BytesProcessed = 1000, ElapsedMilliseconds = 1000, LatencyMilliseconds = 3, JitterMilliseconds = 1 });

        // When a much larger figure arrives, and the Failed column appears with it.
        row.Settle(Transfer(succeeded: 998, failed: 2, latency: Latency(1234, 567)));

        // Then every value is shown whole rather than cut off, and the headings still sit above
        // their own values on one line each - which is a claim about the shape of the output, so
        // the snapshot carries it rather than a line count beside it.
        await Verify(Render(table));
    }
}
