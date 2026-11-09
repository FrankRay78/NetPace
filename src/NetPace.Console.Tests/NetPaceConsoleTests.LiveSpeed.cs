using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// The speed figure the live table carries for each transfer: that it is there, that it is in
    /// the unit the run asked for, and that it is the speed the run went on to measure.
    /// </summary>
    /// <remarks>
    /// Default output has no result line to compare a figure against, so these tests compare the
    /// table's figure with the one <c>--simple</c> reports for the same measurement. That is the
    /// stable format a script reads, so a figure the table showed and a figure a script was given
    /// disagreeing is the failure worth catching.
    /// </remarks>
    public sealed class LiveSpeed
    {
        [Fact]
        public async Task Should_End_The_Live_Figure_On_The_Speed_The_Result_Reports()
        {
            // SCENARIO: Live speed settles on the reported result

            // Given the same measurement shown at a terminal and written for a script.
            var result = await Measure(Stub(), interactive: true, []);
            var simple = await Measure(Stub(), interactive: false, ["--simple"]);

            // Then the figure on each row is the one the run reports for that test. A snapshot
            // cannot hold this: it stays green if both figures move together.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(SimpleFigure("Download", simple.Output), RowFor("Downloading", result.Output));
            Assert.Contains(SimpleFigure("Upload", simple.Output), RowFor("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_The_Live_Figure_In_The_Requested_Unit_And_Scale()
        {
            // SCENARIO: Live speed follows the unit options

            // Given a gigabit download and a half-gigabit upload, so a fixed Mega scale reads clearly.
            string[] unitSwitches = ["--unit", "bytes", "--unit-system", "IEC", "--unit-scale", "Mega"];

            var result = await Measure(Gigabit(), interactive: true, unitSwitches);
            var simple = await Measure(Gigabit(), interactive: false, ["--simple", .. unitSwitches]);

            // Then the table's figure carries the same unit and scale as the figure a script is
            // given; the snapshot carries which unit that was.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains(SimpleFigure("Download", simple.Output), RowFor("Downloading", result.Output));
            Assert.Contains(SimpleFigure("Upload", simple.Output), RowFor("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Write_The_Simple_Lines_And_No_Table_When_Not_Interactive()
        {
            // SCENARIO: Output outside a terminal is unchanged

            // Given a destination that cannot redraw.
            var result = await Measure(Stub(), interactive: false, []);

            // Then nothing of the table reaches it, and the snapshot carries what did.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Downloading", result.Output);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_No_Row_And_No_Figure_When_Download_Is_Switched_Off()
        {
            // Given
            var result = await Measure(Stub(), interactive: true, ["--no-download"]);
            var simple = await Measure(Stub(), interactive: false, ["--simple", "--no-download"]);

            // Then the download has no row at all, and the upload still carries its figure.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Downloading", result.Output);
            Assert.Contains(SimpleFigure("Upload", simple.Output), RowFor("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_No_Row_And_No_Figure_When_Upload_Is_Switched_Off()
        {
            // Given
            var result = await Measure(Stub(), interactive: true, ["--no-upload"]);
            var simple = await Measure(Stub(), interactive: false, ["--simple", "--no-upload"]);

            // Then the upload has no row at all, and the download still carries its figure.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Uploading", result.Output);
            Assert.Contains(SimpleFigure("Download", simple.Output), RowFor("Downloading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_Its_Own_Live_Figures_For_Each_Run_Under_Count()
        {
            // Given successive runs that measure different speeds.
            var result = await Measure(ScriptedRuns(), interactive: true, ["--count", "2"]);

            // Then each run carries its own figures in its own table.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_Its_Own_Live_Figures_For_Each_Run_Under_Loop()
        {
            // Given a loop that cancels itself after two runs, measuring different speeds each time.
            var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(2, cancellationTokenSource);

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(ScriptedRuns());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--loop"], cancellationTokenSource.Token);

            // Then each run carries its own figures in its own table.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_The_Same_Screen_When_A_Measurement_Outlasts_The_Displays_Refresh_Interval()
        {
            // SCENARIO: Live figure is read from the final frame when the display would redraw mid-test

            // Given the same measurement run twice: once instantly, and once taking several times
            // Spectre.Console's own redraw interval to finish.
            // Regression: issue #490 - the live displays redrew themselves while a slow measurement
            // was in flight, stacking an extra frame into the capture, so the snapshots in this file
            // failed intermittently.

            // When
            var instant = await MeasureWithPause(TimeSpan.Zero);
            var slow = await MeasureWithPause(AcrossTheRefreshInterval);

            // Then the screen the user is left with is the one the instant run produced, and it
            // still carries both figures, so the two are not equal by rendering nothing.
            Assert.Equal(0, instant.ExitCode);
            Assert.Equal(0, slow.ExitCode);
            Assert.Equal(instant.Output, slow.Output);
            Assert.Contains("1 Mbps", RowFor("Downloading", slow.Output));
            Assert.Contains("500 Kbps", RowFor("Uploading", slow.Output));
        }

        /// <summary>
        /// Long enough to outlast a live display's redraw interval - 100 ms in Spectre.Console
        /// 0.57.1 - so a paused phase crosses it whatever the machine's speed.
        /// </summary>
        private static readonly TimeSpan AcrossTheRefreshInterval = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Runs one speed test against <paramref name="service"/>, at a terminal or at a
        /// destination that cannot redraw.
        /// </summary>
        private static async Task<TestResult> Measure(ISpeedTestService service, bool interactive, string[] arguments)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            var host = interactive
                ? GetInteractiveCommandLineTestHost(services)
                : GetCommandLineTestHost(services);

            return await host.RunAsync(arguments);
        }

        /// <summary>
        /// Runs one interactive speed test whose server selection, download and upload each take
        /// <paramref name="pause"/> to complete, so a test can hold both live displays open past
        /// their refresh interval without depending on how fast the machine is.
        /// </summary>
        private static async Task<TestResult> MeasureWithPause(TimeSpan pause)
        {
            SpeedTestResult MeasuredSlowly(long bytes)
            {
                Thread.Sleep(pause);
                return Measured(bytes, elapsedMilliseconds: 1000);
            }

            var service = new ScriptedSpeedTester
            {
                ReportsProgress = true,
                ServerSelectionPause = pause,
                DownloadFactory = _ => MeasuredSlowly(bytes: 125_000),
                UploadFactory = _ => MeasuredSlowly(bytes: 62_500),
            };

            return await Measure(service, interactive: true, []);
        }

        private static SpeedTestStub Stub() => new();

        private static SpeedTestResult Measured(long bytes, long elapsedMilliseconds) =>
            new() { BytesProcessed = bytes, ElapsedMilliseconds = elapsedMilliseconds, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null };

        /// <summary>A gigabit download beside a half-gigabit upload.</summary>
        private static ScriptedSpeedTester Gigabit() => new()
        {
            ReportsProgress = true,
            DownloadFactory = _ => Measured(bytes: 125_000_000, elapsedMilliseconds: 1000),
            UploadFactory = _ => Measured(bytes: 62_500_000, elapsedMilliseconds: 1000),
        };

        /// <summary>
        /// A service whose second run is twice as fast as its first, so one run's figures cannot be
        /// mistaken for another's.
        /// </summary>
        private static ScriptedSpeedTester ScriptedRuns() => new()
        {
            ReportsProgress = true,
            DownloadFactory = call => Measured(bytes: 125_000 * (call + 1), elapsedMilliseconds: 1000),
            UploadFactory = call => Measured(bytes: 62_500 * (call + 1), elapsedMilliseconds: 1000),
        };

        /// <summary>
        /// The table row for the named test, read off the rendered screen. A capture of several
        /// runs holds one such row per run, and this reads the first.
        /// </summary>
        private static string RowFor(string test, string output)
        {
            var line = output
                .Split('\n')
                .FirstOrDefault(candidate => candidate.Contains(test, StringComparison.Ordinal));

            Assert.NotNull(line);

            return line;
        }

        /// <summary>
        /// The speed a <c>--simple</c> run reports for the named test, so a figure in the table can
        /// be compared against the number a script is given rather than against a literal.
        /// </summary>
        private static string SimpleFigure(string label, string output)
        {
            var figures = output
                .Split('\n', StringSplitOptions.TrimEntries)
                .Where(line => line.StartsWith($"{label}:", StringComparison.Ordinal))
                .Select(line => line[(label.Length + 1)..].Trim())
                .ToArray();

            return Assert.Single(figures);
        }
    }
}
