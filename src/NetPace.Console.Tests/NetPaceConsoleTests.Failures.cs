using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for surfacing transfer failures: counts appear in every output
/// format, the exit code reflects only NetPace's health by default, and <c>--fail-on</c> opts in to
/// a failure exit code.
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
        public async Task All_Upload_Requests_Failed_Exits_Zero_By_Default()
        {
            // SCENARIO: A measured test whose every request failed exits 0 by default (AC5)

            // Given a server is found and latency measures, but every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When (default settings)
            var result = await host.RunAsync([]);

            // Then the process succeeds and the failure is visible as a count annotation on the
            // upload token.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [InlineData("Total")]
        [InlineData("Partial")]
        public async Task FailOn_Exits_One_When_The_Measurement_Never_Ran(string failOn)
        {
            // A measurement that never completed is worse than an all-failed one, so it has to
            // satisfy the stricter Partial threshold as well as Total.

            // Given discovery throws, so nothing is measured.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("No such host is known"),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--fail-on", failOn]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task Network_IOException_Exits_Zero_By_Default()
        {
            // Regression: HttpIOException derives from System.IO.IOException, so a type-based
            // operational-fault check misread a reset connection as NetPace's own fault and exited 1.

            // Given the latency probe fails the way a dropped connection surfaces.
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely"),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--server", "http://unreachable.example/upload.php"]);

            // Then the network condition is reported as data, not as a NetPace fault.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("The response ended prematurely", result.Output);
        }

        [Fact]
        public async Task Unwritable_Output_File_Reports_The_Path_Literally()
        {
            // Regression: the top-level handler passed the exception message to Spectre.Console
            // unescaped, so a path containing square brackets was parsed as markup and crashed.

            // Given a --file target that cannot be opened for writing, whose path contains markup
            // characters.
            var unwritable = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "out[1].csv");
            var host = HostWith(new ScriptedSpeedTester());

            // When
            var result = await host.RunAsync(["--file", unwritable]);

            // Then the path renders literally and the process reports the fault.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("out[1].csv", result.Output);
        }

        [Fact]
        public async Task Partial_Failure_Is_Annotated_On_The_Token()
        {
            // SCENARIO: Normal + verbosity gradation - partial failure (AC9)

            // Given some download requests fail but the test still measured throughput.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.Partial(150, 5) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync([]);

            // Then the token names how many of the requests failed.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("5 of 150 requests failed", result.Output);
        }

        [Fact]
        public async Task Json_All_Failed_Self_Describes_Through_The_Counts()
        {
            // SCENARIO: Machine formats self-describe on stdout - JSON (AC8a)

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then the JSON carries the counts alongside a zero speed - the schema stays the same
            // shape whether or not the test succeeded, matching normal and CSV output.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Json_Omits_Only_The_Fields_Of_A_Test_That_Did_Not_Run()
        {
            // SCENARIO: Machine formats self-describe on stdout - JSON (AC8a)

            // Given the upload test is skipped and every download request fails.
            var service = new ScriptedSpeedTester { DownloadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--json", "--no-upload"]);

            // Then the skipped upload contributes no fields at all, while the all-failed download
            // reports a zero speed and its counts - the two outcomes stay distinguishable.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Csv_All_Failed_Row_Distinguishes_Total_Failure()
        {
            // SCENARIO: Machine formats self-describe on stdout - CSV (AC8b)

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--csv"]);

            // Then the data row carries UploadSucceeded=0 and UploadFailed=32 beside the zero
            // speed.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Minimal_All_Failed_Annotates_The_Token()
        {
            // SCENARIO: Normal + verbosity gradation - Minimal (AC9)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--verbosity", "Minimal"]);

            // Then the token annotation carries the failure.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("32 of 32 requests failed", result.Output);
        }

        [Fact]
        public async Task Debug_Annotates_The_Token()
        {
            // SCENARIO: Normal + verbosity gradation - Debug (AC9)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--verbosity", "Debug"]);

            // Then Debug reports the same counts as normal verbosity - the failure is described
            // by the counts, not by per-request detail.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("32 of 32 requests failed", result.Output);
        }

        [Fact]
        public async Task FailOn_Total_Exits_One_On_All_Failed_Test()
        {
            // SCENARIO: --fail-on total opt-in (AC10)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--fail-on", "Total"]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task FailOn_None_Exits_Zero_On_All_Failed_Test()
        {
            // SCENARIO: --fail-on total opt-in, default is none (AC10)

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When (explicit default)
            var result = await host.RunAsync(["--fail-on", "None"]);

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
            var result = await host.RunAsync(["--fail-on", "Partial"]);

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
            var result = await host.RunAsync(["--fail-on", "Total"]);

            // Then
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Json_Debug_Reports_Only_The_Counts()
        {
            // Debug verbosity adds no per-request detail to a machine format.

            // Given every upload request fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--json", "--verbosity", "Debug"]);

            // Then the JSON carries the counts and nothing else.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"UploadFailed\":32", result.Output);
        }

        [Fact]
        public async Task Quiet_All_Failed_Signals_Through_The_Exit_Code()
        {
            // --quiet asks for no output at all, so the counts go unseen. --fail-on is how a quiet
            // consumer detects failure.

            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When
            var result = await host.RunAsync(["--quiet", "--fail-on", "Total"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
        }

        [Fact]
        public async Task Unreachable_User_Specified_Server_Exits_Zero()
        {
            // An unreachable --server is a network condition, not a NetPace fault: it must exit 0.

            // Given a user-specified server whose latency probe throws (host down).
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new HttpRequestException("Could not open socket"),
            };
            var host = HostWith(mock);

            // When run with the default latency probe enabled.
            var result = await host.RunAsync(["--server", "http://unreachable.example/upload.php"]);

            // Then the reason reaches the console and the process exits 0 (not 1).
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Could not open socket", result.Output);
        }

        [Fact]
        public async Task Operational_Fault_During_A_Run_Exits_One()
        {
            // The one deliberate deviation from main: a network condition is reported and exits 0,
            // but a fault in NetPace itself must still fail the process rather than be swallowed.

            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new IOException("disk gone"),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task FailOn_Is_FailFast_Under_Count()
        {
            // SCENARIO: --fail-on is uniform and fail-fast (AC12)

            // Given the first iteration already all-fails.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };
            var host = HostWith(service);

            // When run with --count under --fail-on total.
            var result = await host.RunAsync(["--count", "5", "--fail-on", "Total", "--verbosity", "Minimal"]);

            // Then it exits 1 at the first triggering measurement - the snapshot carries one
            // result line, not five.
            Assert.Equal(1, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task FailOn_Is_FailFast_When_The_Measurement_Never_Ran()
        {
            // SCENARIO: --fail-on is uniform and fail-fast (AC12)

            // Given discovery throws on every iteration.
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("No such host is known"),
            };
            var host = HostWith(mock);

            // When run with --count under --fail-on total.
            var result = await host.RunAsync(["--count", "5", "--fail-on", "Total", "--verbosity", "Minimal"]);

            // Then the process stops at the first iteration rather than reporting all five.
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(1, result.Output.Split("No such host is known").Length - 1);
        }
    }
}
