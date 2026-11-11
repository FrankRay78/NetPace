using System.Globalization;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// CLI behaviour shared by the three ways a run can be asked for: once, <c>--count</c> times, or
    /// on a <c>--loop</c> until stopped. Every rule a run obeys is checked here against each mode it
    /// applies to, so a rule that held in one mode and not another fails this set.
    /// </summary>
    /// <remarks>
    /// The rules are what a test succeeding does, what a failed test does with and without
    /// <c>--fail-on</c>, what the <c>--fail-on</c> threshold does, what a cancellation does during a
    /// test and during the pause between tests, and what an operational fault does. Timeout wording
    /// and output shape are not this set's business: snapshots live beside the format they belong to.
    /// </remarks>
    public sealed class RunModes
    {
        /// <summary>A single run: no <c>--count</c> and no <c>--loop</c>.</summary>
        private const string Single = "single";

        /// <summary>A run that stops after a fixed number of tests.</summary>
        private const string Counted = "count";

        /// <summary>A run that tests until something stops it.</summary>
        private const string Looping = "loop";

        /// <summary>A request that failed on its own merits.</summary>
        private const string Network = "network";

        /// <summary>
        /// A request abandoned at its own timeout, which NetPace composes its own line for. It
        /// arrives as a cancellation, so it is the failure shape most likely to be mistaken for the
        /// one the user asked for, and every rule below is checked against it as well as against a
        /// plain network failure.
        /// </summary>
        private const string Timeout = "timeout";

        private const string ServerUrl = "http://slow.example/upload.php";

        /// <summary>
        /// How many tests a run here performs: what <c>--count</c> is given, and how many a looping
        /// run is allowed before its pause ends it. More than two, so a rule that ends a run one
        /// test late is still visible.
        /// </summary>
        private const int TestsPerRun = 3;

        /// <summary>
        /// The line a measured test writes under <c>--simple</c>, counted to tell how many tests ran.
        /// </summary>
        private const string ResultLine = "Ping: 24 ms";

        private static int Occurrences(string text, string value) => text.Split(value).Length - 1;

        private static LatencyTestResult Latency(string serverUrl) =>
            new()
            {
                Server = new Server { Sponsor = "Deutsche Telekom", Location = "Frankfurt", Url = serverUrl },
                LatencyMilliseconds = 24,
                MinimumLatencyMilliseconds = null,
                MaximumLatencyMilliseconds = null,
                JitterMilliseconds = 3
            };

        /// <summary>
        /// What ends one test when the failure shape is <paramref name="shape"/>. A factory rather
        /// than an instance, so each test of a repeated run throws its own.
        /// </summary>
        private static Func<Exception> FailureOf(string shape) => shape switch
        {
            Timeout => () => new TaskCanceledException(
                "The request was canceled due to the configured timeout of 2 seconds elapsing.",
                new TimeoutException("A task was canceled.")),
            _ => () => new HttpRequestException("Could not open socket")
        };

        /// <summary>
        /// What the user reads about a failure of shape <paramref name="shape"/>: the exception's own
        /// message, or the composed line a timeout gets instead.
        /// </summary>
        private static string ReportedBy(string shape) =>
            shape == Timeout ? "did not respond in time" : "Could not open socket";

        /// <summary>
        /// A service whose latency probe throws on the probes <paramref name="throwsOn"/> selects,
        /// counted from one, and measures normally on the rest.
        /// </summary>
        /// <remarks>
        /// The probe is where a test is abandoned whole. A failure raised later in the test would
        /// leave a part-written result behind, and the result lines would no longer be countable.
        /// </remarks>
        private static ISpeedTestService ServiceWhoseProbeThrows(Func<int, bool> throwsOn, Func<Exception> fault)
        {
            var probes = 0;
            return new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => throwsOn(Interlocked.Increment(ref probes))
                    ? throw fault()
                    : Task.FromResult(Latency(url)),
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(150)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(ScriptedSpeedTester.Clean(32))
            };
        }

        /// <summary>
        /// A run the user cancels while a test is in flight: the probe cancels the given source and
        /// then observes the token, as the real one does.
        /// </summary>
        private static ISpeedTestService ServiceCancelledMidTest(CancellationTokenSource cancellation) =>
            new SpeedTestMock
            {
                GetServerLatencyByServerUrlAsyncFunc = (_, _, token) =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult(Latency(ServerUrl));
                }
            };

        private static string[] ArgsFor(string mode, string[] extra) => mode switch
        {
            Counted => ["--server-url", ServerUrl, "--count", TestsPerRun.ToString(CultureInfo.InvariantCulture), "--simple", .. extra],
            Looping => ["--server-url", ServerUrl, "--loop", "--simple", .. extra],
            _ => ["--server-url", ServerUrl, "--simple", .. extra]
        };

        /// <summary>
        /// Runs <paramref name="service"/> in one run mode.
        /// </summary>
        /// <param name="mode">Which of the three ways to ask for a run.</param>
        /// <param name="service">The service under the run, built against the run's own cancellation source.</param>
        /// <param name="pausesBeforeCancelling">
        /// How many pauses between tests may elapse before the run is cancelled. A looping run never
        /// ends on its own, so this is what stops one; in the other modes it is a bound, turning a
        /// rule gone wrong into a failed assertion rather than a test that never returns.
        /// </param>
        /// <param name="extra">Further options to pass.</param>
        private static async Task<TestResult> RunAsync(string mode, Func<CancellationTokenSource, ISpeedTestService> service, int pausesBeforeCancelling, params string[] extra)
        {
            using var cancellation = new CancellationTokenSource();

            var services = new ServiceCollection();
            services.AddSingleton(service(cancellation));
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter>(new SelfCancellingWaiter(pausesBeforeCancelling, cancellation));

            return await new CommandLineTestHost(services).RunAsync(ArgsFor(mode, extra), cancellation.Token);
        }

        /// <inheritdoc cref="RunAsync(string, Func{CancellationTokenSource, ISpeedTestService}, int, string[])" />
        private static Task<TestResult> RunAsync(string mode, ISpeedTestService service, int pausesBeforeCancelling, params string[] extra) =>
            RunAsync(mode, _ => service, pausesBeforeCancelling, extra);

        /// <summary>The <c>--fail-on</c> options for a run that either opted in or did not.</summary>
        private static string[] FailOn(bool failOn) => failOn ? ["--fail-on", "Total"] : [];

        [Theory]
        [InlineData(Single)]
        [InlineData(Counted)]
        [InlineData(Looping)]
        public async Task Successful_Test_Reports_Its_Result(string mode)
        {
            // Given a server that measures normally.
            // When the run is asked for in this mode.
            var result = await RunAsync(mode, new ScriptedSpeedTester(), TestsPerRun);

            // Then every test it asked for reported a result, and the run succeeded.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(mode == Single ? 1 : TestsPerRun, Occurrences(result.Output, ResultLine));
        }

        [Theory]
        [InlineData(Single, Network)]
        [InlineData(Single, Timeout)]
        [InlineData(Counted, Network)]
        [InlineData(Counted, Timeout)]
        [InlineData(Looping, Network)]
        [InlineData(Looping, Timeout)]
        public async Task Failed_Test_Is_Reported_And_The_Run_Carries_On(string mode, string shape)
        {
            // SCENARIO: A failed test is reported and the run carries on
            // SCENARIO: A timed-out request does not end a repeated run

            // Given a server whose first test fails and whose later tests measure normally.
            var service = ServiceWhoseProbeThrows(probe => probe == 1, FailureOf(shape));

            // When the run is asked for in this mode, without --fail-on.
            var result = await RunAsync(mode, service, TestsPerRun);

            // Then the failure is reported once, the run succeeded, and every test after the first
            // still reported a result - a single run, having no later test, reports none.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, Occurrences(result.Output, ReportedBy(shape)));
            Assert.Equal(mode == Single ? 0 : TestsPerRun - 1, Occurrences(result.Output, ResultLine));
        }

        [Theory]
        [InlineData(Single, Network)]
        [InlineData(Single, Timeout)]
        [InlineData(Counted, Network)]
        [InlineData(Counted, Timeout)]
        [InlineData(Looping, Network)]
        [InlineData(Looping, Timeout)]
        public async Task Failed_Test_Under_FailOn_Ends_The_Run(string mode, string shape)
        {
            // SCENARIO: --fail-on ends a looping run on the first failure

            // Given a server whose first test fails and whose later tests measure normally.
            var service = ServiceWhoseProbeThrows(probe => probe == 1, FailureOf(shape));

            // When the run is asked for in this mode, under --fail-on.
            var result = await RunAsync(mode, service, TestsPerRun, "--fail-on", "Total");

            // Then it stopped at that failure and exited 1: no later test reported a result.
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(1, Occurrences(result.Output, ReportedBy(shape)));
            Assert.Equal(0, Occurrences(result.Output, ResultLine));
        }

        [Theory]
        [InlineData(Single, "Total")]
        [InlineData(Single, "Partial")]
        [InlineData(Counted, "Total")]
        [InlineData(Counted, "Partial")]
        [InlineData(Looping, "Total")]
        [InlineData(Looping, "Partial")]
        public async Task FailOn_Threshold_Ends_The_Run(string mode, string threshold)
        {
            // SCENARIO: --fail-on ends a looping run on the first failure

            // Given every test measures, but every upload request within it fails - which trips both
            // thresholds.
            var service = new ScriptedSpeedTester { UploadFactory = _ => ScriptedSpeedTester.AllFailed(32) };

            // When the run is asked for in this mode, under that threshold.
            var result = await RunAsync(mode, service, TestsPerRun, "--fail-on", threshold);

            // Then it stopped at the first triggering test and exited 1.
            Assert.Equal(1, result.ExitCode);
            Assert.Equal(1, Occurrences(result.Output, ResultLine));
        }

        [Theory]
        [InlineData(Single, false)]
        [InlineData(Single, true)]
        [InlineData(Counted, false)]
        [InlineData(Counted, true)]
        [InlineData(Looping, false)]
        [InlineData(Looping, true)]
        public async Task Cancelling_During_A_Test_Ends_The_Run_Quietly(string mode, bool failOn)
        {
            // SCENARIO: Cancelling a looping run exits cleanly
            // SCENARIO: Ctrl-C still ends the run quietly

            // Given a run the user cancels while a test is in flight. --fail-on reports a failed
            // measurement, and a run the user stopped is not one, so the outcome is the same either
            // way.
            // When
            var result = await RunAsync(mode, ServiceCancelledMidTest, TestsPerRun, FailOn(failOn));

            // Then nothing is reported as an error and the run exits 0.
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [InlineData(Counted, false)]
        [InlineData(Counted, true)]
        [InlineData(Looping, false)]
        [InlineData(Looping, true)]
        public async Task Cancelling_During_The_Pause_Ends_The_Run_Quietly(string mode, bool failOn)
        {
            // SCENARIO: Cancelling a looping run exits cleanly
            // SCENARIO: Ctrl-C still ends the run quietly

            // A single run has no pause between tests, so this rule is the two repeating modes' only.

            // Given a run the user cancels during the pause after its first test.
            // When
            var result = await RunAsync(mode, new ScriptedSpeedTester(), 1, FailOn(failOn));

            // Then the test that did run reported its result, nothing is reported as an error, and
            // the run exits 0.
            Assert.Equal(1, Occurrences(result.Output, ResultLine));
            Assert.DoesNotContain("Error:", result.Output);
            Assert.Equal(0, result.ExitCode);
        }

        [Theory]
        [InlineData(Single)]
        [InlineData(Counted)]
        [InlineData(Looping)]
        public async Task Cancellation_The_User_Did_Not_Ask_For_Is_Reported(string mode)
        {
            // Only the run's own token says a cancellation is the user's. One the user never asked
            // for - here with no TimeoutException inside it to say what it was - is a test that ended
            // without a result, and ending the run silently on it was the original bug in another
            // shape.

            // Given a probe that is cancelled while the user's token stays live.
            var service = ServiceWhoseProbeThrows(_ => true, () => new OperationCanceledException());

            // When the run is asked for in this mode, under --fail-on.
            var result = await RunAsync(mode, service, TestsPerRun, "--fail-on", "Total");

            // Then the failure is reported and the run exits 1.
            Assert.Contains("Error:", result.Output);
            Assert.Equal(1, result.ExitCode);
        }

        [Theory]
        [InlineData(Single)]
        [InlineData(Counted)]
        [InlineData(Looping)]
        public async Task Operational_Fault_Ends_The_Run(string mode)
        {
            // SCENARIO: An operational fault ends a looping run

            // Given a test that meets an operational fault rather than a network condition.
            var service = ServiceWhoseProbeThrows(_ => true, () => new IOException("There is not enough space on the disk."));

            // When the run is asked for in this mode.
            var result = await RunAsync(mode, service, TestsPerRun);

            // Then the run ends with a non-zero exit code rather than reporting the fault as data and
            // carrying on.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(0, Occurrences(result.Output, ResultLine));
        }

        [Theory]
        [InlineData(Counted)]
        [InlineData(Looping)]
        public async Task Operational_Fault_After_The_First_Result_Ends_The_Run(string mode)
        {
            // SCENARIO: An operational fault ends a looping run

            // A fault that arrives once a run is under way is the one a repeating mode could swallow:
            // it has a result behind it, so carrying on would look like a healthy run.

            // Given a run whose first test measures and whose second meets an operational fault.
            var service = ServiceWhoseProbeThrows(probe => probe == 2, () => new IOException("There is not enough space on the disk."));

            // When the run is asked for in this mode.
            var result = await RunAsync(mode, service, TestsPerRun);

            // Then the run stopped there, with a non-zero exit code and only the first result behind
            // it.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Equal(1, Occurrences(result.Output, ResultLine));
        }
    }
}
