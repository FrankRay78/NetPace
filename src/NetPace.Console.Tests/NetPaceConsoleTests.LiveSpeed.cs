using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class LiveSpeed
    {
        [Fact]
        public async Task Should_End_The_Live_Figure_On_The_Speed_The_Result_Line_Reports()
        {
            // SCENARIO: Live speed settles on the reported result

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the figure beside each bar is the one the result line reports for that test. A
            // snapshot cannot hold this: it stays green if both figures move together.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(FigureOnResultLine("Download", result.Output), FigureBeside("Downloading", result.Output));
            Assert.Equal(FigureOnResultLine("Upload", result.Output), FigureBeside("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_The_Live_Figure_In_The_Requested_Unit_And_Scale()
        {
            // SCENARIO: Live speed follows the unit options

            // Given a gigabit download and a half-gigabit upload, so a fixed Mega scale reads clearly.
            var service = new ScriptedSpeedTester
            {
                ReportsProgress = true,
                DownloadFactory = _ => Measured(bytes: 125_000_000, elapsedMilliseconds: 1000),
                UploadFactory = _ => Measured(bytes: 62_500_000, elapsedMilliseconds: 1000),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--unit", "BytesPerSecond", "--unit-system", "IEC", "--unit-scale", "Mega"]);

            // Then the live figure carries the same unit and scale as the result line; the snapshot
            // carries which unit that was.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(FigureOnResultLine("Download", result.Output), FigureBeside("Downloading", result.Output));
            Assert.Equal(FigureOnResultLine("Upload", result.Output), FigureBeside("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Print_The_Result_Line_And_No_Live_Display_When_Not_Interactive()
        {
            // SCENARIO: Output outside a terminal is unchanged

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the result line is still printed, and no live display precedes it. The snapshot
            // carries both facts, and is byte-identical to the non-interactive runs elsewhere in
            // this suite.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_No_Bar_And_No_Live_Figure_When_Download_Is_Switched_Off()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--no-download"]);

            // Then the download has neither a bar nor a figure, and the upload still has both.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(FigureOnResultLine("Upload", result.Output), FigureBeside("Uploading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_No_Bar_And_No_Live_Figure_When_Upload_Is_Switched_Off()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--no-upload"]);

            // Then the upload has neither a bar nor a figure, and the download still has both.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(FigureOnResultLine("Download", result.Output), FigureBeside("Downloading", result.Output));
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_Its_Own_Live_Figures_For_Each_Run_Under_Count()
        {
            // Given successive runs that measure different speeds.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(ScriptedRuns());
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--count", "2"]);

            // Then each run carries its own figures beside its own bars.
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

            // Then each run carries its own figures beside its own bars.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        private static SpeedTestResult Measured(long bytes, long elapsedMilliseconds) =>
            new() { BytesProcessed = bytes, ElapsedMilliseconds = elapsedMilliseconds, RequestsSucceeded = 1, RequestsFailed = 0 };

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
        /// The speed figure shown beside the first bar for the named test, read off the rendered
        /// screen: whatever follows the percentage on that bar's line. A capture of several runs
        /// holds one such bar per run, and this reads the first.
        /// </summary>
        private static string FigureBeside(string description, string output)
        {
            var line = output
                .Split('\n')
                .Select(candidate => candidate.Trim())
                .FirstOrDefault(candidate => candidate.StartsWith(description, StringComparison.Ordinal));

            Assert.NotNull(line);

            var percentage = line.IndexOf('%', StringComparison.Ordinal);
            Assert.True(percentage >= 0, $"No percentage found on the '{description}' line: '{line}'");

            return line[(percentage + 1)..].Trim();
        }

        /// <summary>
        /// The speed the result line reports for the named test, so a live figure can be compared
        /// against the number the user is finally given rather than against a hard-coded literal.
        /// </summary>
        private static string FigureOnResultLine(string label, string output)
        {
            var line = output
                .Split('\n')
                .FirstOrDefault(candidate => candidate.Contains("Latency:", StringComparison.Ordinal));

            Assert.NotNull(line);

            var figures = line.Split(',', StringSplitOptions.TrimEntries)
                .Where(part => part.StartsWith($"{label}:", StringComparison.Ordinal))
                .Select(part => part[(label.Length + 1)..].Trim())
                .ToArray();

            return Assert.Single(figures);
        }
    }
}
