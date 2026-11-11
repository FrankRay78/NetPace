namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// CLI behaviour when a request is abandoned at its own timeout rather than failing on its own
    /// merits: a timeout is reported like any other network failure, honours <c>--fail-on</c>, and is
    /// told apart from the cancellation the user asked for.
    /// </summary>
    /// <remarks>
    /// What a timeout does to a run that repeats, and how it is told apart from a cancellation in
    /// each run mode, belong to <see cref="RunModes"/>: those are rules of the run block, checked
    /// there against every mode at once and against a plain network failure as well as a timeout.
    /// This class holds what is particular to a timeout - the line composed for it, and the places
    /// it can surface from.
    /// </remarks>
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

        private static CommandLineTestHost HostWith(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        /// <summary>
        /// A server that never answers its latency probe in time, so every test ends at the timeout.
        /// </summary>
        private static CommandLineTestHost HostWhoseProbeTimesOut() =>
            HostWith(new SpeedTestMock { GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => throw TimedOut() });

        [Theory]
        [InlineData("default", "")]
        [InlineData("minimal", "--minimal")]
        [InlineData("simple", "--simple")]
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
    }
}
