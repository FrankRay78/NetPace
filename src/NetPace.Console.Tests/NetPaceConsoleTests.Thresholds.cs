using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for the target a scheduled run checks itself against: <c>--min-download</c>,
/// <c>--min-upload</c> and <c>--max-latency</c>. A missed target names itself on the error stream
/// and sets an exit code of its own, while the result the run writes is untouched.
/// </summary>
/// <remarks>
/// The stub measures the same figures every run: an 8 Kbps download (1,000 bytes in one second), a
/// 100 ms idle latency, and an upload a shade under 18.67 Kbps. Every target below is chosen
/// against those.
/// </remarks>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Thresholds
    {
        /// <summary>
        /// The exit code a missed target reports. An interface contract a monitoring check reads,
        /// so it is named exactly rather than read back off the code under test.
        /// </summary>
        private const int MissedExitCode = 3;

        /// <summary>
        /// The exit code a refusal the command line alone decides reports, named here for the same
        /// reason.
        /// </summary>
        private const int UsageExitCode = 2;

        private static CommandLineTestHost StubHost() => GetCommandLineTestHost(StubServices());

        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return GetCommandLineTestHost(services);
        }

        [Fact]
        public async Task A_Download_Below_Its_Minimum_Exits_With_The_Threshold_Code()
        {
            // SCENARIO: A download below its minimum exits with the threshold code

            // Given a minimum download far above what the run will measure.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps"]);

            // Then the run reports the miss on the error stream and exits with the threshold code.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Run_Meeting_Every_Threshold_Exits_As_Usual()
        {
            // SCENARIO: A run meeting every threshold exits as usual

            // Given targets the run comfortably meets.
            var withThresholds = StubHost();
            var withoutThresholds = StubHost();

            // When
            var gated = await withThresholds.RunAsync(["--simple", "--min-download", "1Kbps", "--min-upload", "1Kbps", "--max-latency", "500ms"]);
            var plain = await withoutThresholds.RunAsync(["--simple"]);

            // Then nothing is added anywhere and the exit code is the one the same run reports
            // without the targets.
            Assert.Equal(plain.ExitCode, gated.ExitCode);
            Assert.Equal(0, gated.ExitCode);
            Assert.Equal(string.Empty, gated.NoticeOutput);
            Assert.Equal(plain.Output, gated.Output);
        }

        [Fact]
        public async Task A_Latency_Above_Its_Maximum_Is_Reported()
        {
            // SCENARIO: A latency above its maximum is reported

            // Given a maximum latency below the idle latency the run will measure.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--max-latency", "40ms"]);

            // Then
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task Every_Missed_Threshold_Is_Named()
        {
            // SCENARIO: Every missed threshold is named

            // Given minimums for download and upload that the run misses both of.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps", "--min-upload", "10Mbps"]);

            // Then one line names each.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Theory]
        [InlineData("--min-download", "50")]
        [InlineData("--min-upload", "50")]
        [InlineData("--max-latency", "40")]
        [InlineData("--min-download", "50furlongs")]
        [InlineData("--min-download", "Mbps")]
        [InlineData("--min-download", "0Mbps")]
        [InlineData("--min-download", "2,5Mbps")]
        [InlineData("--max-latency", "40s")]
        [InlineData("--max-latency", "0ms")]
        [InlineData("--min-download", "9999999999999999999999Pbps")]
        [InlineData("--min-download", "50MBPS")]
        public async Task A_Threshold_Without_A_Usable_Value_Is_Refused(string option, string value)
        {
            // SCENARIO: A threshold with no unit is refused

            // Given a threshold value NetPace cannot read.
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync([option, value]);

            // Then it is refused as a usage error before anything is measured.
            Assert.Equal(UsageExitCode, result.ExitCode);
            Assert.Empty(serviceCalls);
            await Verify(result.ErrorOutput).UseParameters(option, value);
        }

        [Theory]
        [InlineData("--loop")]
        [InlineData("--count")]
        public async Task Thresholds_Are_Refused_With_A_Repeating_Run(string repeat)
        {
            // SCENARIO: Thresholds are refused with a repeating run

            // Given a threshold asked for alongside a run that repeats.
            var (host, serviceCalls) = HostRecordingMeasurements();
            string[] args = repeat == "--count"
                ? ["--min-download", "50Mbps", "--count", "2"]
                : ["--min-download", "50Mbps", "--loop"];

            // A refusal that stopped working would leave --loop running forever, so the run is
            // stopped after a while instead: it then exits 0, and the test fails rather than hangs.
            using var giveUp = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // When
            var result = await host.RunAsync(args, giveUp.Token);

            // Then it is refused before anything is measured, and the user is told where repeated
            // checks belong.
            Assert.Equal(UsageExitCode, result.ExitCode);
            Assert.Empty(serviceCalls);
            await Verify(result.ErrorOutput).UseParameters(repeat);
        }

        [Fact]
        public async Task A_Single_Count_Is_Accepted_Alongside_A_Threshold()
        {
            // A count of one is a single run however it was spelled, so only a count above one is
            // refused.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--count", "1", "--min-download", "50Mbps"]);

            // Then
            Assert.Equal(MissedExitCode, result.ExitCode);
        }

        [Theory]
        [InlineData("--min-download", "50Mbps", "--no-download")]
        [InlineData("--min-upload", "50Mbps", "--no-upload")]
        [InlineData("--max-latency", "40ms", "--no-latency")]
        public async Task A_Threshold_On_A_Test_That_Is_Turned_Off_Is_Refused(string option, string value, string switchedOff)
        {
            // Given a threshold for a test the same run turns off.
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync([option, value, switchedOff]);

            // Then it is refused before anything is measured.
            Assert.Equal(UsageExitCode, result.ExitCode);
            Assert.Empty(serviceCalls);
            await Verify(result.ErrorOutput).UseParameters(option, value, switchedOff);
        }

        [Fact]
        public async Task A_Test_With_No_Figure_Misses_Its_Threshold()
        {
            // SCENARIO: A test with no figure misses its threshold

            // Given a download test in which every request fails, so the figure it measures is zero.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.AllFailed(150) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps"]);

            // Then the threshold is reported as missed.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Run_That_Reached_No_Server_Misses_Every_Threshold()
        {
            // Given discovery cannot be reached at all, so no test produced a figure.
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("No such host is known"),
            };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps", "--max-latency", "40ms"]);

            // Then both targets are reported as missed.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Figure_Exactly_At_Its_Target_Meets_It()
        {
            // Given targets set to exactly the figures the run measures: an 8 Kbps download and a
            // 100 ms idle latency.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "8Kbps", "--max-latency", "100ms"]);

            // Then neither is missed.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.NoticeOutput);
        }

        [Theory]
        [InlineData("8Kbps", 0)]      // exactly the measured figure, so the target is met
        [InlineData("8kbps", 0)]      // the scale prefix is read in any case
        [InlineData("8Kibps", 3)]     // IEC steps in 1024s, so 8 Kibps is 8,192 bps
        [InlineData("1KBps", 0)]      // a byte is eight bits, so 1 KBps is 8,000 bps
        [InlineData("2KBps", 3)]
        [InlineData("0.008Mbps", 0)]  // decimals are read as written
        [InlineData("0.009Mbps", 3)]
        public async Task A_Threshold_Is_Read_In_The_Unit_It_Names(string threshold, int expectedExitCode)
        {
            // Given a download minimum either side of the 8 Kbps the run measures, written in
            // several of the units NetPace displays speeds in.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", threshold]);

            // Then
            Assert.Equal(expectedExitCode, result.ExitCode);
        }

        /// <summary>
        /// Every speed unit suffix <c>NetPace.Core</c> writes, across unit, unit system and scale.
        /// Read off the library rather than listed here, so a suffix the library gains or renames
        /// shows up as a threshold value the CLI refuses.
        /// </summary>
        public static TheoryData<string> EverySpeedSuffixTheLibraryWrites()
        {
            var measured = new SpeedTestResult { BytesProcessed = 1, ElapsedMilliseconds = 1, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null };

            var data = new TheoryData<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (SpeedUnit unit in Enum.GetValues<SpeedUnit>())
            {
                foreach (SpeedUnitSystem unitSystem in Enum.GetValues<SpeedUnitSystem>())
                {
                    foreach (SpeedScale scale in Enum.GetValues<SpeedScale>())
                    {
                        (_, string suffix) = measured.GetSpeedStringParts(unit, unitSystem, scale);

                        if (seen.Add(suffix))
                        {
                            data.Add(suffix);
                        }
                    }
                }
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(EverySpeedSuffixTheLibraryWrites))]
        public async Task Every_Speed_Unit_The_Library_Writes_Is_Accepted(string suffix)
        {
            // Given a threshold written in a unit the library itself writes speeds in.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", "--min-download", $"1{suffix}"]);

            // Then the value is read rather than refused: whether the run then met the target or
            // missed it, it was never a usage error.
            Assert.NotEqual(UsageExitCode, result.ExitCode);
        }

        [Fact]
        public async Task A_Threshold_Is_Read_The_Same_Whatever_The_Unit_Options_Say()
        {
            // Given the same target read under display options that scale, systematise and
            // re-base every speed the run shows.
            var plain = StubHost();
            var reUnited = StubHost();

            // When
            var expected = await plain.RunAsync(["--simple", "--min-download", "50Mbps"]);
            var actual = await reUnited.RunAsync(["--simple", "--min-download", "50Mbps", "--unit", "bytes", "--unit-system", "IEC", "--unit-scale", "giga"]);

            // Then the target was read, missed and reported identically.
            Assert.Equal(expected.ExitCode, actual.ExitCode);
            Assert.Equal(MissedExitCode, actual.ExitCode);
            Assert.Equal(expected.NoticeOutput, actual.NoticeOutput);
        }

        [Theory]
        [MemberData(nameof(NetPaceConsoleTests.EveryOutputModeOfASpeedTest), MemberType = typeof(NetPaceConsoleTests))]
        public async Task The_Result_Is_Unchanged_By_A_Missed_Threshold(string outputMode)
        {
            // Given the same run with and without a target it misses.
            var withThreshold = StubHost();
            var withoutThreshold = StubHost();
            string[] mode = outputMode.Length == 0 ? [] : [outputMode];

            // When
            var missed = await withThreshold.RunAsync([.. mode, "--min-download", "50Mbps"]);
            var plain = await withoutThreshold.RunAsync(mode);

            // Then the result is the same text in every output mode, and only the exit code and
            // the error stream differ.
            Assert.Equal(plain.Output, missed.Output);
            Assert.Equal(MissedExitCode, missed.ExitCode);
        }

        [Fact]
        public async Task A_Missed_Threshold_Is_Reported_Under_Quiet()
        {
            // Given a run told to say nothing.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--quiet", "--min-download", "50Mbps"]);

            // Then the result is suppressed but the miss is not.
            Assert.Equal(string.Empty, result.Output);
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Latency_Above_Its_Maximum_Is_Reported_In_Default_Output()
        {
            // Given a maximum below the idle latency the run measures, with no format flag at a
            // terminal that animates, so the figure comes from the writer that feeds the live
            // display rather than from the one the line-based formats share.
            var host = GetInteractiveCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--max-latency", "40ms"]);

            // Then the miss names the latency the run measured, not an absent one.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Latency_Within_Its_Maximum_Exits_Zero_In_Default_Output()
        {
            // Given a maximum above the idle latency the run measures, with no format flag at a
            // terminal that animates, where the live display's writer supplies the figure.
            var host = GetInteractiveCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync(["--max-latency", "200ms"]);

            // Then the target is met: the measured latency reached the check, so nothing is
            // reported as missed.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.NoticeOutput);
        }

        [Fact]
        public async Task A_Near_Miss_Is_Reported_Without_Rounding_Onto_Its_Target()
        {
            // Given a download a thousandth of a megabit under its minimum, which is closer to
            // the target than the two decimal places a speed is usually written to.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => new SpeedTestResult { BytesProcessed = 320_375, ElapsedMilliseconds = 1_000, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null }
            };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "2.564Mbps"]);

            // Then both figures carry the decimals it takes for the measured one to read below
            // the target it missed.
            Assert.Equal(MissedExitCode, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task A_Missed_Threshold_Follows_The_Records_Of_The_Test_That_Missed_It()
        {
            // Given a missed target under --diagnostics, with the records and the miss lines
            // sharing one stream, as they do on standard error.
            var shared = new StringWriter();
            var services = StubServices();
            services.AddSingleton(new NetPace.Console.Diagnostics.DiagnosticOutput(shared));
            services.AddSingleton(new NoticeOutput(shared));
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple", "--diagnostics", "--min-download", "50Mbps"]);

            // Then the stream reads what the test measured before what it fell short of: only the
            // record of how the run ended, which carries the exit code the miss decided, follows it.
            Assert.Equal(MissedExitCode, result.ExitCode);
            string[] lines = shared.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            int miss = Array.FindIndex(lines, line => line.StartsWith("Download ", StringComparison.Ordinal));
            Assert.True(miss > 0, "The miss line was not written after any record.");
            Assert.All(lines[(miss + 1)..], line => Assert.Contains("event=run.end", line, StringComparison.Ordinal));
        }

        [Theory]
        [InlineData("--max-latency", "99.5ms")]
        [InlineData("--min-download", "8.0004Kbps")]
        public async Task A_Fractional_Target_Just_Past_The_Measured_Figure_Is_Missed(string option, string value)
        {
            // Given a target a fraction of a whole millisecond under the 100 ms latency, or a
            // fraction of a whole bit per second over the 8,000 bps download, so rounding the
            // target to the nearest whole unit would land it on the measured figure.
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--simple", option, value]);

            // Then the figure is on the wrong side of the target the user typed, so it is missed.
            Assert.Equal(MissedExitCode, result.ExitCode);
        }

        [Fact]
        public async Task A_FailOn_Trigger_Exits_Four_Even_When_A_Threshold_Was_Missed()
        {
            // Given a download test whose every request failed, with both --fail-on and a target
            // that the zero figure misses.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.AllFailed(150) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--fail-on", "total", "--min-download", "50Mbps"]);

            // Then --fail-on decides the exit code, with its own, and the user still hears what
            // the line fell short of.
            Assert.Equal(4, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task An_Operational_Fault_Exits_One_Alongside_A_Threshold()
        {
            // Given a --file target that cannot be opened for writing, alongside a target. The
            // file fails before anything is measured, so no target is checked: the fault reaches
            // the top-level handler past the check whatever the targets would have said.
            var unwritable = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "out.csv");
            var host = StubHost();

            // When
            var result = await host.RunAsync(["--file", unwritable, "--min-download", "50Mbps"]);

            // Then the fault decides the exit code, as it does today.
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task A_Cancelled_Run_Writes_No_Miss_Lines()
        {
            // Given a run the user stops while discovery is still out. The stop is raised by
            // discovery itself, so the ordering is settled by the test rather than by how long
            // the suite's host takes to reach it.
            using var cancellation = new CancellationTokenSource();
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                }
            };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--simple", "--min-download", "50Mbps"], cancellation.Token);

            // Then nothing was measured to compare, so nothing is reported and the run ends as a
            // cancelled run does today.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.NoticeOutput);
        }
    }
}
