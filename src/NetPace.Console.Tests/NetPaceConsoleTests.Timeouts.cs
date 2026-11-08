using System.Net.Http;

namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour when a request is abandoned at its own timeout rather than failing on its own
/// merits: a timeout is reported like any other network failure, honours <c>--fail-on</c>, and is
/// told apart from the cancellation the user asked for.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class Timeouts
    {
        private const string ServerUrl = "http://slow.example/upload.php";

        /// <summary>
        /// What a request abandoned at its own timeout raises: .NET's HTTP client reports its own
        /// timeout as a cancellation carrying a <see cref="TimeoutException"/>, and NetPace.Core's
        /// latency probe reports its per-probe timeout the same way.
        /// </summary>
        private static Exception TimedOut() =>
            new TaskCanceledException("A task was canceled.", new TimeoutException("The operation was canceled."));

        private static LatencyTestResult Latency(string serverUrl) =>
            new()
            {
                Server = new Server { Sponsor = "Deutsche Telekom", Location = "Frankfurt", Url = serverUrl },
                LatencyMilliseconds = 24,
                JitterMilliseconds = 3
            };

        private static CommandLineTestHost HostWith(ISpeedTestService service, IWaiter? waiter = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton(waiter ?? new NoDelayStub());
            return new CommandLineTestHost(services);
        }

        [Theory]
        [InlineData("")]
        [InlineData("--minimal")]
        [InlineData("--csv")]
        [InlineData("--json")]
        public async Task Timed_Out_Probe_Is_Reported_In_Every_Output_Mode(string formatFlag)
        {
            // SCENARIO: A timed-out request is reported

            // Given a server that does not answer its latency probe in time.
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw TimedOut(),
            };
            var host = HostWith(mock);

            // When the test runs in each output mode.
            string[] args = formatFlag.Length == 0
                ? ["--server", ServerUrl]
                : ["--server", ServerUrl, formatFlag];
            var result = await host.RunAsync(args);

            // Then the user is told why the test failed, and the exit code stays 0 - a network
            // condition is data, not a NetPace fault.
            Assert.Contains("did not respond in time", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Timed_Out_Probe_Says_Nothing_About_The_Cancellation_Behind_It()
        {
            // One composed line explains the failure: the exception's own wording ("A task was
            // canceled") names no cause and would leave the user no better off than silence.

            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw TimedOut(),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--server", ServerUrl]);

            // Then the composed line is what the user reads, with no cancellation wording beside it.
            Assert.Contains("did not respond in time", result.Output);
            Assert.DoesNotContain("canceled", result.Output);
            Assert.DoesNotContain("cancelled", result.Output);
        }

        [Theory]
        [InlineData("Total")]
        [InlineData("Partial")]
        public async Task Timed_Out_Probe_Trips_FailOn(string failOn)
        {
            // SCENARIO: A timed-out request trips fail-on

            // Given a server that does not answer its latency probe in time.
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw TimedOut(),
            };
            var host = HostWith(mock);

            // When the test runs with --fail-on.
            var result = await host.RunAsync(["--server", ServerUrl, "--fail-on", failOn]);

            // Then the failure is reported and the run exits 1.
            Assert.Contains("did not respond in time", result.Output);
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task Timed_Out_Server_List_Fetch_Is_Reported()
        {
            // The server-list fetch times out the same way the probe does - .NET's HTTP client
            // raises its own timeout as a cancellation carrying a TimeoutException - so it must
            // reach the user by the same route.

            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw TimedOut(),
            };
            var host = HostWith(mock);

            // When run without --server, so discovery runs.
            var result = await host.RunAsync([]);

            // Then
            Assert.Contains("did not respond in time", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Timed_Out_Probe_Does_Not_End_A_Repeated_Run()
        {
            // SCENARIO: A timed-out request does not end a repeated run

            // Given a server whose first latency probe times out and whose later probes answer.
            var probe = 0;
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => Interlocked.Increment(ref probe) == 1
                    ? throw TimedOut()
                    : Task.FromResult(Latency(url)),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32)),
            };
            var host = HostWith(mock);

            // When three tests are asked for.
            var result = await host.RunAsync(["--server", ServerUrl, "--count", "3", "--minimal"]);

            // Then the timeout is reported once and the two remaining tests still report results.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, result.Output.Split("did not respond in time").Length - 1);
            Assert.Equal(2, result.Output.Split("Latency: 24 ms").Length - 1);
        }

        [Fact]
        public async Task Timed_Out_Probe_Stops_A_Repeated_Run_Under_FailOn()
        {
            // --fail-on is fail-fast everywhere else, so a timeout must stop a repeated run at the
            // first failure rather than carrying on through it.

            var probe = 0;
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => Interlocked.Increment(ref probe) == 1
                    ? throw TimedOut()
                    : Task.FromResult(Latency(url)),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32)),
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--server", ServerUrl, "--count", "3", "--minimal", "--fail-on", "Total"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("Latency: 24 ms", result.Output);
        }

        [Fact]
        public async Task Cancelling_A_Test_Ends_The_Run_Quietly()
        {
            // SCENARIO: Ctrl-C still ends the run quietly

            // Given a run the user cancels while the test is in progress.
            using var cancellation = new CancellationTokenSource();
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, token) =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(Latency(ServerUrl));
                },
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--server", ServerUrl], cancellation.Token);

            // Then nothing is reported as an error and the run exits 0.
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Cancelling_During_The_Pause_Ends_The_Run_Quietly()
        {
            // SCENARIO: Ctrl-C still ends the run quietly

            // Given a looping run the user cancels during the pause between tests.
            using var cancellation = new CancellationTokenSource();
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => Task.FromResult(Latency(url)),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32)),
            };
            var host = HostWith(mock, new SelfCancellingWaiter(1, cancellation));

            // When
            var result = await host.RunAsync(["--server", ServerUrl, "--loop", "--minimal"], cancellation.Token);

            // Then
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Cancelling_Mid_Test_Reports_Nothing_Even_Under_FailOn()
        {
            // --fail-on reports a failed measurement, and a run the user stopped is not one.

            using var cancellation = new CancellationTokenSource();
            var mock = new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, token) =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(Latency(ServerUrl));
                },
            };
            var host = HostWith(mock);

            // When
            var result = await host.RunAsync(["--server", ServerUrl, "--fail-on", "Total"], cancellation.Token);

            // Then
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
