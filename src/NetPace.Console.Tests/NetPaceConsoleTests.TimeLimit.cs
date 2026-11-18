using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class TimeLimit
    {
        [Theory]
        [InlineData("5", "plain-number")]
        [InlineData("600", "plain-seconds")]
        [InlineData("soon", "words")]
        [InlineData("00:00:00", "zero")]
        [InlineData("-00:01:00", "negative")]
        [InlineData("10:00", "minutes-and-seconds")]
        [InlineData("25:00:00", "past-a-day")]
        [InlineData("1.00:00:00", "days")]
        public async Task TimeLimit_NotAPositiveDuration_IsRejectedBeforeAnyTestRuns(string value, string valueName)
        {
            // SCENARIO: An unusable limit is rejected

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--time-limit", value]);

            // Then the run is refused up front and nothing is measured. A plain number is refused
            // along with the rest: .NET reads a bare 5 as five days, and 10:00 as ten hours, so
            // accepting either would set a limit nobody asked for.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(serviceCalls);

            // No result line appears, and the error stream holds the message that has to say what
            // a valid value looks like.
            Assert.Empty(result.Output);
            await Verify(result.ErrorOutput).UseTextForParameters(valueName);
        }

        [Theory]
        [InlineData("00:00:30")]
        [InlineData("01:00:00")]
        public async Task TimeLimit_WrittenAsHoursMinutesAndSeconds_IsAccepted(string value)
        {
            // SCENARIO: The limit can be changed

            // Given
            var services = StubServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--time-limit", value, "--simple"]);

            // Then the value binds and the run goes ahead, so a validator written one way round
            // would be caught here rather than by a user.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Download:", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Help_NamesTheTimeLimitAndItsDefault()
        {
            // Given
            var services = StubServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--help"]);

            // Then the option and the duration a user gets without it are both on the help screen,
            // because the default is the whole of what protects a scheduled run.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("--time-limit", result.Output, StringComparison.Ordinal);
            Assert.Contains("00:10:00", result.Output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--simple", "simple")]
        [InlineData("--minimal", "minimal")]
        [InlineData("--csv", "csv")]
        [InlineData("--json", "json")]
        [InlineData("", "default")]
        public async Task ARunCutShortAtTheLimit_ReportsWhatItMeasuredAndSaysNothingAboutTheLimit(string formatSwitch, string formatName)
        {
            // SCENARIO: A run that reaches the limit reports what it measured
            // SCENARIO: Tests not reached before the limit are not run

            // Given a download slow enough that the run reaches its limit part way through it.
            var (services, limit, tester) = CutShortServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(formatSwitch.Length == 0 ? [] : [formatSwitch]);

            // Then the run ends on its own terms: the download figure it did measure is reported,
            // the upload it never reached is reported as a skipped test is, and nothing anywhere
            // in the output mentions the limit. The whole output is the snapshot, so a message
            // about the limit appearing in any format would fail here.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(0, tester.UploadCalls);
            Assert.Single(limit.Started);

            await Verify(result.Output).UseTextForParameters(formatName);
        }

        [Theory]
        [InlineData("total")]
        [InlineData("partial")]
        public async Task ARunCutShortAtTheLimit_WithNoFailedRequest_DoesNotTripFailOn(string threshold)
        {
            // SCENARIO: A run cut short at the limit does not trip fail-on

            // Given
            var (services, _, _) = CutShortServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple", "--fail-on", threshold]);

            // Then a request stopped at the limit is neither a success nor a failure, so the
            // strictest threshold is untroubled by it: nothing failed, so nothing is reported as
            // having failed.
            Assert.Equal(0, result.ExitCode);
        }

        [Fact]
        public async Task EachRunOfARepeatedTest_GetsTheLimitAfresh()
        {
            // SCENARIO: The limit applies to each run of a repeated test

            // Given two runs with a delay between them, where only the first reaches the limit.
            var (services, limit, tester) = CutShortServices();
            tester.CutsShortRun = run => run == 0;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--count", "2", "--delay", "00:05:00", "--simple"]);

            // Then the second run is whole: it reaches its upload test, which means the first
            // run's limit did not carry over and the five minutes waited between them did not
            // count against it either.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, tester.UploadCalls);
            Assert.Equal(2, limit.Started.Count);
            Assert.Equal(limit.Started[0], limit.Started[1]);

            await Verify(result.Output);
        }

        [Fact]
        public async Task TheDelayBetweenRuns_IsNotCountedTowardsEitherRunsLimit()
        {
            // SCENARIO: The limit applies to each run of a repeated test

            // Given three runs with a delay between each.
            var (services, limit, tester) = CutShortServices();
            tester.CutsShortRun = _ => false;
            var waiter = new LimitWatchingWaiter(limit);
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            await host.RunAsync(["--count", "3", "--delay", "00:05:00", "--simple"]);

            // Then every wait happened with no run's limit running, so none of it was billed to a
            // run.
            Assert.Equal(2, waiter.LimitRunningAtEachWait.Count);
            Assert.All(waiter.LimitRunningAtEachWait, Assert.False);
        }

        [Fact]
        public async Task ARunCutShortAtTheLimit_WithAFailedRequest_StillTripsFailOn()
        {
            // Given a run cut short at the limit after one of its requests had genuinely failed.
            var (services, _, tester) = CutShortServices();
            tester.CutShortFailures = 1;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple", "--fail-on", "partial"]);

            // Then the limit hides nothing: the exit code is the one the failure earns.
            Assert.Equal(1, result.ExitCode);
        }

        [Fact]
        public async Task ATransferCutShortBeforeAnyDataArrived_IsReportedAsATestThatDidNotRun()
        {
            // Given a run that reaches the limit during a download that has delivered nothing.
            var (services, _, tester) = CutShortServices();
            tester.CutShortBytes = 0;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then no download figure is invented from no data: zero would read as a dead link.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Download:", result.Output, StringComparison.Ordinal);
            Assert.Contains("Ping:", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ATransferCutShortBeforeAnyDataArrived_AtATerminal_LeavesNoRowForIt()
        {
            // Given
            var (services, _, tester) = CutShortServices();
            tester.CutShortBytes = 0;
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the download row goes, as the row of a test that never ran does.
            Assert.Equal(0, result.ExitCode);

            await Verify(result.Output);
        }

        [Fact]
        public async Task AScreeningProbeStoppedAtTheLimit_IsRecordedNamingTheTimeLimit()
        {
            // Given a run whose limit is reached while candidates are still being screened.
            var (services, _, tester) = CutShortServices();
            tester.Stage = CutShortStage.Screening;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple", "--diagnostics"]);

            // Then the probe the limit cut off names the limit, not a cancellation by the user.
            Assert.Contains("reason=\"run time limit reached\"", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("cancelled by caller", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task WithNoOptionGiven_TheRunIsBoundedByTenMinutes()
        {
            // Given
            var (services, limit, _) = CutShortServices();
            var host = GetCommandLineTestHost(services);

            // When
            await host.RunAsync(["--simple"]);

            // Then the run is bounded without the user having asked for it, by the duration
            // --help names as the default.
            Assert.Equal(TimeSpan.FromMinutes(10), Assert.Single(limit.Started));
        }

        [Fact]
        public async Task WithAShorterLimitGiven_TheRunIsBoundedByTheDurationAsked()
        {
            // SCENARIO: The limit can be changed

            // Given
            var (services, limit, _) = CutShortServices();
            var host = GetCommandLineTestHost(services);

            // When
            await host.RunAsync(["--time-limit", "00:00:45", "--simple"]);

            // Then
            Assert.Equal(TimeSpan.FromSeconds(45), Assert.Single(limit.Started));
        }

        [Fact]
        public async Task ARequestStoppedAtTheLimit_IsRecordedAsCancelledNamingTheTimeLimit()
        {
            // Given
            var (services, _, _) = CutShortServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple", "--diagnostics"]);

            // Then the stopped request reads as cancelled with a reason naming the limit rather
            // than the library's own generic wording, and it is counted among the download test's
            // cancelled requests, so the full account of a cut-short run is there for whoever
            // captures it.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("status=cancelled", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("reason=\"run time limit reached\"", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("stopped by caller", result.DiagnosticOutput, StringComparison.Ordinal);
            Assert.Contains("event=test.end test=download requests=2 succeeded=1 failed=0 cancelled=1", result.DiagnosticOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ARunThatFinishesWithinTheLimit_WritesWhatItWroteBefore()
        {
            // Given a run no limit interferes with.
            var (services, _, tester) = CutShortServices();
            tester.CutsShortRun = _ => false;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then every test runs and every figure is reported: the limit is invisible to a run
            // that finishes inside it.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(1, tester.UploadCalls);

            await Verify(result.Output);
        }

        [Fact]
        public async Task ARunCutShortAtTheLimit_AtATerminal_LeavesNoRowForTheTestItNeverReached()
        {
            // SCENARIO: Tests not reached before the limit are not run

            // Given a run watched at a terminal, where the table carries a row per test from
            // before any of them has started.
            var (services, _, _) = CutShortServices();
            var host = GetInteractiveCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([]);

            // Then the upload row is gone rather than left spinning or crossed out: the test did
            // not fail, it never ran, and the table reads as it does for a test nobody asked for.
            Assert.Equal(0, result.ExitCode);

            await Verify(result.Output);
        }

        [Fact]
        public async Task TheLimitReachedWhileStillChoosingAServer_EndsTheRunWithNoServer()
        {
            // Given a run whose limit is reached before a server has been settled on.
            var (services, _, tester) = CutShortServices();
            tester.Stage = CutShortStage.ChoosingServer;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then the run ends as one with no server to measure against already does, message
            // included: there is nothing about the limit to add, because a run with no server
            // reads the same whatever stopped the search. A network outcome, so the exit code is
            // untouched.
            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);

            await Verify(result.ErrorOutput);
        }

        [Fact]
        public async Task TheLimitReachedBeforeAnyProbeAnswered_ReportsNoLatencyFigureAtAll()
        {
            // Given a run whose limit is reached during the latency test, with nothing answered.
            var (services, _, tester) = CutShortServices();
            tester.Stage = CutShortStage.Latency;
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--simple"]);

            // Then no figure is invented for a measurement that took no probes: the latency test
            // is reported as a skipped one is, and so are the transfers the run never reached.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(0, tester.UploadCalls);

            await Verify(result.Output);
        }

        /// <summary>
        /// The registrations a cut-short run needs: a limit a test reaches on demand, and a speed
        /// tester that reaches it during its download test.
        /// </summary>
        private static (IServiceCollection Services, RecordingRunTimeLimit Limit, CutShortSpeedTester Tester) CutShortServices()
        {
            var limit = new RecordingRunTimeLimit();
            var tester = new CutShortSpeedTester(limit);

            var services = new ServiceCollection();
            services.AddSingleton<IRunTimeLimit>(limit);
            services.AddSingleton<ISpeedTestService>(tester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            return (services, limit, tester);
        }

        /// <summary>
        /// A wait between runs that takes no time, and notes whether a run's limit was running
        /// when it happened.
        /// </summary>
        private sealed class LimitWatchingWaiter(RecordingRunTimeLimit limit) : IWaiter
        {
            public List<bool> LimitRunningAtEachWait { get; } = [];

            public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                LimitRunningAtEachWait.Add(limit.IsRunning);
                return Task.CompletedTask;
            }
        }
    }
}
