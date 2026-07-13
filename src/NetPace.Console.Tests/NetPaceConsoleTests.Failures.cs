namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for surfacing transfer failures (issue #206): counts appear in every output
/// format, the exit code reflects only NetPace's health by default, standard error carries the
/// human notice, and <c>--fail-on</c> opts in to a failure exit code.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Failures
    {
        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        [Fact]
        public async Task Total_Network_Failure_Exits_Zero_By_Default()
        {
            // SCENARIO: Total network failure exits 0 by default (AC5)

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When (default settings)
            var result = await host.RunAsync([]);

            // Then the process succeeds, the failure is visible in stdout, and stderr carries guidance.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("32 of 32 requests failed", result.Output);
            Assert.Contains("Upload failed: all 32 requests to", result.Error);
        }

        [Fact]
        public async Task Partial_Failure_Is_Annotated_On_Stdout_Without_Stderr_Notice()
        {
            // SCENARIO: Normal + verbosity gradation - partial failure (AC9)

            // Given some download requests fail but the dimension still measured throughput.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([]);

            // Then the token is annotated on stdout, but partial failure gets no stderr notice.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("5 of 150 requests failed", result.Output);
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task Json_All_Failed_Self_Describes_On_Stdout_With_No_Stderr()
        {
            // SCENARIO: Machine formats self-describe on stdout - JSON (AC8a)

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--json" ]);

            // Then the JSON carries the counts and no upload speed value; nothing is written to stderr.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"UploadSucceeded\":0", result.Output);
            Assert.Contains("\"UploadFailed\":32", result.Output);
            Assert.DoesNotContain("\"UploadSpeed\"", result.Output);
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task Csv_All_Failed_Row_Distinguishes_Total_Failure_With_No_Stderr()
        {
            // SCENARIO: Machine formats self-describe on stdout - CSV (AC8b)

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--csv" ]);

            // Then the data row shows UploadSucceeded=0; nothing is written to stderr.
            Assert.Equal(0, result.ExitCode);
            var dataRow = result.Output.Split('\n')[1];
            Assert.Contains(",0,32,", dataRow); // UploadSucceeded=0, UploadFailed=32
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task Minimal_All_Failed_Annotates_Token_Without_Stderr_Notice()
        {
            // SCENARIO: Normal + verbosity gradation - Minimal (AC9)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--verbosity", "Minimal" ]);

            // Then the token annotation carries the failure; Minimal emits no separate stderr notice.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("32 of 32 requests failed", result.Output);
            Assert.Empty(result.Error);
        }

        [Fact]
        public async Task Debug_Streams_Each_Failure_Reason_To_Stderr()
        {
            // SCENARIO: Normal + verbosity gradation - Debug (AC9)

            var service = new ScriptedSpeedTester
            {
                UploadFactory = _ => ScriptedSpeedTester.AllFailed(32),
                StreamedFailureReason = "Connection reset by peer"
            };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--verbosity", "Debug" ]);

            // Then the raw reason is emitted live on stderr, in addition to the total-failure notice.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Upload request failed: Connection reset by peer", result.Error);
            Assert.Contains("Upload failed: all 32 requests to", result.Error);
        }

        [Fact]
        public async Task FailOn_Total_Exits_One_On_All_Failed_Dimension()
        {
            // SCENARIO: --fail-on total opt-in (AC10)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--fail-on", "Total" ]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task FailOn_None_Exits_Zero_On_All_Failed_Dimension()
        {
            // SCENARIO: --fail-on total opt-in, default is none (AC10)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When (explicit default)
            var result = await host.RunAsync([ "--fail-on", "None" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task FailOn_Partial_Exits_One_When_Any_Request_Failed()
        {
            // SCENARIO: --fail-on partial opt-in (AC11)

            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--fail-on", "Partial" ]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task FailOn_Total_Exits_Zero_On_Partial_Failure()
        {
            // SCENARIO: --fail-on partial opt-in, total does not trip on partial (AC11)

            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([ "--fail-on", "Total" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Unreachable_User_Specified_Server_Exits_Zero_Regardless_Of_Latency()
        {
            // An unreachable --server is a network condition, not a NetPace fault: it must exit 0
            // whether or not the latency probe runs (the latency probe is in the selection region).

            // Given a user-specified server whose latency probe throws (host down).
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new HttpRequestException("Could not open socket"),
            };
            var host = HostWith(mock);

            // When run with the default latency probe enabled.
            var result = await host.RunAsync([ "--server", "http://unreachable.example/upload.php" ]);

            // Then the condition is reported on stderr and the process exits 0 (not 1).
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("No speed test servers were found.", result.Error);
        }

        [Fact]
        public async Task FailOn_Is_FailFast_Under_Count()
        {
            // SCENARIO: --fail-on is uniform and fail-fast (AC12)

            // Given the first iteration already all-fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When run with --count under --fail-on total.
            var result = await host.RunAsync([ "--count", "5", "--fail-on", "Total", "--verbosity", "Minimal" ]);

            // Then the process exits 1 at the first triggering measurement.
            Assert.Equal(1, result.ExitCode);
        }
    }
}
