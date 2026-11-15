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
        public async Task TimeLimit_NotAPositiveDuration_IsRejectedBeforeAnyTestRuns(string value, string valueName)
        {
            // SCENARIO: An unusable limit is rejected

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--time-limit", value]);

            // Then the run is refused up front and nothing is measured. A plain number is refused
            // along with the rest: .NET reads a bare 5 as five days, so accepting it would set a
            // limit nobody asked for.
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
    }
}
