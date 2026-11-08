namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// CLI behaviour when a request is abandoned at its own timeout rather than failing on its own
    /// merits: a timeout is reported like any other network failure, honours <c>--fail-on</c>, and is
    /// told apart from the cancellation the user asked for.
    /// </summary>
    public sealed class Timeouts
    {
        private const string ServerUrl = "http://slow.example/upload.php";

        /// <summary>
        /// What a request abandoned at its own timeout raises: .NET's HTTP client reports its own
        /// timeout as a cancellation carrying a <see cref="TimeoutException"/>, and NetPace.Core's
        /// latency probe reports its per-probe timeout the same way.
        /// </summary>
        private static Exception TimedOut() =>
            new TaskCanceledException(
                "The request was canceled due to the configured timeout of 2 seconds elapsing.",
                new TimeoutException("A task was canceled."));

        private static LatencyTestResult Latency(string serverUrl) =>
            new()
            {
                Server = new Server { Sponsor = "Deutsche Telekom", Location = "Frankfurt", Url = serverUrl },
                LatencyMilliseconds = 24,
                MinimumLatencyMilliseconds = null,
                MaximumLatencyMilliseconds = null,
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

        /// <summary>
        /// A server that never answers its latency probe in time, so every test ends at the timeout.
        /// </summary>
        private static CommandLineTestHost HostWhoseProbeTimesOut() =>
            HostWith(new SpeedTestMock { GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw TimedOut() });

        /// <summary>
        /// A server whose first latency probe times out and whose later probes answer, so a repeated
        /// run meets one timeout and then measures normally.
        /// </summary>
        private static CommandLineTestHost HostWhoseFirstProbeTimesOut(IWaiter? waiter = null)
        {
            var probe = 0;
            return HostWith(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => Interlocked.Increment(ref probe) == 1
                    ? throw TimedOut()
                    : Task.FromResult(Latency(url)),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32))
            },
            waiter);
        }

        /// <summary>
        /// A run the user cancels while the latency probe is in flight: the probe cancels the given
        /// source and then observes the token, as the real one does.
        /// </summary>
        private static CommandLineTestHost HostCancelledMidProbe(CancellationTokenSource cancellation) =>
            HostWith(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, token) =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(Latency(ServerUrl));
                }
            });

        [Theory]
        [InlineData("default", "")]
        [InlineData("minimal", "--minimal")]
        [InlineData("csv", "--csv")]
        [InlineData("json", "--json")]
        public async Task Timed_Out_Probe_Is_Reported_In_Every_Output_Mode(string mode, string formatFlag)
        {
            // SCENARIO: A timed-out request is reported

            // Given a server that does not answer its latency probe in time.
            var host = HostWhoseProbeTimesOut();

            // When the test runs in each output mode.
            string[] args = formatFlag.Length == 0
                ? ["--server-url", ServerUrl]
                : ["--server-url", ServerUrl, formatFlag];
            var result = await host.RunAsync(args);

            // Then the whole of what each mode emits is the reviewed contract: a substring match
            // would pass wherever the line landed, and what this has to hold is that the machine
            // readable modes carry the failure the same way every other network failure does. The
            // exit code stays 0 - a network condition is data, not a NetPace fault.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(mode);
        }

        [Fact]
        public async Task Timed_Out_Probe_Says_Nothing_About_The_Cancellation_Behind_It()
        {
            // One composed line explains the failure: the exception's own wording names the
            // configured timeout that elapsed, which describes NetPace's own plumbing and leaves
            // the word "canceled" in front of a user who cancelled nothing.

            // Given
            var host = HostWhoseProbeTimesOut();

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl]);

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
            var host = HostWhoseProbeTimesOut();

            // When the test runs with --fail-on.
            var result = await host.RunAsync(["--server-url", ServerUrl, "--fail-on", failOn]);

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

            // Given
            var host = HostWith(new SpeedTestMock { GetServersAsyncFunc = _ => throw TimedOut() });

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
            var host = HostWhoseFirstProbeTimesOut();

            // When three tests are asked for.
            var result = await host.RunAsync(["--server-url", ServerUrl, "--count", "3", "--minimal"]);

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

            // Given
            var host = HostWhoseFirstProbeTimesOut();

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--count", "3", "--minimal", "--fail-on", "Total"]);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("Latency: 24 ms", result.Output);
        }

        [Fact]
        public async Task Timed_Out_Probe_Does_Not_End_A_Continuous_Run()
        {
            // SCENARIO: A timed-out request does not end a repeated run

            // --loop keeps its own copy of the catch-and-report block, separate from the one
            // --count uses, so a timeout swallowed there would end a monitoring loop silently -
            // the original failure, in the mode it hurt most.

            // Given a server whose first latency probe times out and whose later probes answer,
            // and a pause that ends the loop after the second test.
            using var cancellation = new CancellationTokenSource();
            var host = HostWhoseFirstProbeTimesOut(new SelfCancellingWaiter(2, cancellation));

            // When the run loops until that cancellation.
            var result = await host.RunAsync(["--server-url", ServerUrl, "--loop", "--minimal"], cancellation.Token);

            // Then the timeout is reported once and the run went on to measure normally.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, result.Output.Split("did not respond in time").Length - 1);
            Assert.Equal(1, result.Output.Split("Latency: 24 ms").Length - 1);
        }

        [Fact]
        public async Task Timed_Out_Probe_Stops_A_Continuous_Run_Under_FailOn()
        {
            // --fail-on is fail-fast in --loop as it is in --count, so the first timeout ends the
            // run rather than looping on through it.

            // Given
            using var cancellation = new CancellationTokenSource();
            var host = HostWhoseFirstProbeTimesOut(new SelfCancellingWaiter(2, cancellation));

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--loop", "--minimal", "--fail-on", "Total"], cancellation.Token);

            // Then
            Assert.Equal(1, result.ExitCode);
            Assert.DoesNotContain("Latency: 24 ms", result.Output);
        }

        [Fact]
        public async Task Cancelling_Mid_Test_Ends_A_Continuous_Run_Quietly()
        {
            // SCENARIO: Ctrl-C still ends the run quietly

            // The companion to the two above: --loop's guard must claim the cancellation the user
            // asked for while leaving a timeout to be reported.

            // Given a looping run the user cancels while the probe is in flight.
            using var cancellation = new CancellationTokenSource();
            var host = HostCancelledMidProbe(cancellation);

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--loop", "--minimal"], cancellation.Token);

            // Then nothing is reported as an error and the run exits 0.
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Cancelling_A_Test_Ends_The_Run_Quietly()
        {
            // SCENARIO: Ctrl-C still ends the run quietly

            // Given a run the user cancels while the test is in progress.
            using var cancellation = new CancellationTokenSource();
            var host = HostCancelledMidProbe(cancellation);

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl], cancellation.Token);

            // Then nothing is reported as an error and the run exits 0.
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Cancelling_Mid_Test_Ends_A_Counted_Run_Quietly()
        {
            // SCENARIO: Ctrl-C still ends the run quietly

            // --count keeps its own copy of the mid-test guard, separate from the single-run and
            // --loop ones, so it is pressed mid-test here rather than only during the pause.

            // Given a counted run the user cancels while the probe is in flight.
            using var cancellation = new CancellationTokenSource();
            var host = HostCancelledMidProbe(cancellation);

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--count", "3", "--minimal"], cancellation.Token);

            // Then nothing is reported as an error and the run exits 0.
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [InlineData("single")]
        [InlineData("count")]
        [InlineData("loop")]
        public async Task Cancellation_The_User_Did_Not_Ask_For_Is_Reported(string run)
        {
            // Only the command's own token says a cancellation is the user's. One the user never
            // asked for - here with no TimeoutException inside it to say what it was - is a test
            // that ended without a result, and ending the run silently on it was the original bug
            // in another shape.

            // Given a probe that is cancelled while the user's token stays live.
            var host = HostWith(new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw new OperationCanceledException()
            });

            // When each kind of run meets it under --fail-on.
            string[] args = run switch
            {
                "count" => ["--server-url", ServerUrl, "--count", "3", "--fail-on", "Total"],
                "loop" => ["--server-url", ServerUrl, "--loop", "--fail-on", "Total"],
                _ => ["--server-url", ServerUrl, "--fail-on", "Total"]
            };
            var result = await host.RunAsync(args);

            // Then the failure is reported and the run exits 1.
            Assert.Contains("Error:", result.Output);
            Assert.Equal(1, result.ExitCode);
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
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32))
            };
            var host = HostWith(mock, new SelfCancellingWaiter(1, cancellation));

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--loop", "--minimal"], cancellation.Token);

            // Then
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task Cancelling_Mid_Test_Reports_Nothing_Even_Under_FailOn()
        {
            // --fail-on reports a failed measurement, and a run the user stopped is not one.

            // Given
            using var cancellation = new CancellationTokenSource();
            var host = HostCancelledMidProbe(cancellation);

            // When
            var result = await host.RunAsync(["--server-url", ServerUrl, "--fail-on", "Total"], cancellation.Token);

            // Then
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
