using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Duration
    {
        [Theory]
        [InlineData("--time-limit", "30s")]
        [InlineData("--time-limit", "10m")]
        [InlineData("--time-limit", "2h")]
        [InlineData("--time-limit", "1d")]
        [InlineData("--time-limit", "30d")]
        [InlineData("--time-limit", "00:10:00")]
        [InlineData("--time-limit", "030s")]
        [InlineData("--delay", "30s")]
        [InlineData("--delay", "10m")]
        [InlineData("--delay", "2h")]
        [InlineData("--delay", "1d")]
        [InlineData("--delay", "30d")]
        [InlineData("--delay", "00:10:00")]
        [InlineData("--delay", "030s")]
        public async Task EitherOption_GivenADurationInAnAcceptedForm_RunsTheTest(string option, string value)
        {
            // Given
            var services = StubServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([option, value, "--simple"]);

            // Then the value binds and the run goes ahead. Both options are asked for every form,
            // so one of them gaining or losing a form on its own fails here.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Download:", result.Output, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("--time-limit", "600")]
        [InlineData("--time-limit", "5")]
        [InlineData("--time-limit", "soon")]
        [InlineData("--time-limit", "10:00")]
        [InlineData("--time-limit", "1.00:00:00")]
        [InlineData("--time-limit", "00:00:30.5")]
        [InlineData("--time-limit", "25:00:00")]
        [InlineData("--time-limit", "30S")]
        [InlineData("--time-limit", "-30s")]
        [InlineData("--time-limit", "30sec")]
        [InlineData("--time-limit", "1h30m")]
        [InlineData("--time-limit", "1.5h")]
        [InlineData("--time-limit", "31d")]
        [InlineData("--time-limit", "721h")]
        [InlineData("--time-limit", "9999999999999999999999s")]
        [InlineData("--delay", "600")]
        [InlineData("--delay", "5")]
        [InlineData("--delay", "soon")]
        [InlineData("--delay", "10:00")]
        [InlineData("--delay", "1.00:00:00")]
        [InlineData("--delay", "00:00:30.5")]
        [InlineData("--delay", "25:00:00")]
        [InlineData("--delay", "30S")]
        [InlineData("--delay", "-30s")]
        [InlineData("--delay", "30sec")]
        [InlineData("--delay", "1h30m")]
        [InlineData("--delay", "1.5h")]
        [InlineData("--delay", "31d")]
        [InlineData("--delay", "721h")]
        [InlineData("--delay", "9999999999999999999999s")]
        public async Task EitherOption_GivenADurationItCannotRead_IsRefusedBeforeAnyTestRuns(string option, string value)
        {
            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync([option, value]);

            // Then the run is refused up front and nothing is measured. The same list is put to
            // both options, so a form one of them quietly accepts fails here.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(serviceCalls);
            Assert.Empty(result.Output);
        }

        [Theory]
        [InlineData("600", "bare-number")]
        [InlineData("3600000", "bare-number-past-the-longest")]
        [InlineData("soon", "words")]
        [InlineData("10:00", "minutes-and-seconds")]
        [InlineData("31d", "past-the-longest")]
        public async Task Delay_GivenADurationItCannotRead_SaysWhatToWriteInstead(string value, string valueName)
        {
            // SCENARIO: A bare number is rejected rather than read as days

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--delay", value]);

            // Then the refusal is reported on the error stream, not the output stream.
            Assert.NotEqual(0, result.ExitCode);
            Assert.Empty(serviceCalls);
            Assert.Empty(result.Output);
            await Verify(result.ErrorOutput).UseTextForParameters(valueName);
        }

        [Fact]
        public async Task Delay_GivenZero_IsAccepted()
        {
            // Given
            var services = StubServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--delay", "0s", "--simple"]);

            // Then a delay of nothing is the option's own default, so it cannot be refused.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Download:", result.Output, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ASuffixedTimeLimit_BoundsTheRunByTheDurationItNames()
        {
            // SCENARIO: A unit-suffixed time limit bounds the run

            // Given
            var (suffixed, suffixedLimit) = LimitRecordingHost();
            var (colons, colonLimit) = LimitRecordingHost();

            // When the same thirty seconds is written each way.
            var suffixedResult = await suffixed.RunAsync(["--time-limit", "30s", "--simple"]);
            var colonResult = await colons.RunAsync(["--time-limit", "00:00:30", "--simple"]);

            // Then the run is bounded by the thirty seconds asked for, and the two spellings are
            // the same request: a run under either reports the same thing.
            Assert.Equal(TimeSpan.FromSeconds(30), Assert.Single(suffixedLimit.Started));
            Assert.Equal(suffixedLimit.Started, colonLimit.Started);
            Assert.Equal(colonResult.Output, suffixedResult.Output);
            Assert.Equal(0, suffixedResult.ExitCode);
        }

        [Theory]
        [InlineData("30s", 30)]
        [InlineData("10m", 10 * 60)]
        [InlineData("2h", 2 * 60 * 60)]
        [InlineData("1d", 24 * 60 * 60)]
        [InlineData("30d", 30 * 24 * 60 * 60)]
        [InlineData("00:10:00", 10 * 60)]
        [InlineData("030s", 30)]
        public async Task EachAcceptedForm_BoundsTheRunByTheSecondsItNames(string value, int expectedSeconds)
        {
            // Given
            var (host, limit) = LimitRecordingHost();

            // When
            var result = await host.RunAsync(["--time-limit", value, "--simple"]);

            // Then each unit means what it says, so a unit silently worth a different number of
            // seconds fails here rather than binding a duration nobody asked for.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Assert.Single(limit.Started));
        }

        [Fact]
        public void TheLongestAcceptedDuration_IsOneTheRealLimitAndWaitAccept()
        {
            // Given the ceiling the options advertise, rather than a duration written out here, so
            // raising it past what a timer takes fails here instead of where a run starts.
            var longest = DurationOption.Longest;

            // When it is put to the production limit and wait, not the recording stubs.
            using var started = new RunTimeLimit().Start(longest);
            var waited = new Waiter().Delay(longest, new CancellationToken(canceled: true));

            // Then neither refuses it.
            Assert.False(started.IsCancellationRequested);
            Assert.True(waited.IsCanceled);
        }

        [Fact]
        public async Task ASuffixedDelay_SeparatesRunsByTheDurationItNames()
        {
            // SCENARIO: A unit-suffixed delay separates runs

            // Given
            var waiter = new RecordingWaiter();
            var services = StubServices();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--count", "2", "--delay", "1m", "--simple"]);

            // Then the run is held back by the one minute asked for, once.
            Assert.Equal(0, result.ExitCode);
            Assert.Equal([TimeSpan.FromMinutes(1)], waiter.Waited);
        }

        [Fact]
        public async Task Help_DescribesTheFormsBothOptionsAccept()
        {
            // Given
            var services = StubServices();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--help"]);

            // Then each option says on its own line which forms it takes, so nobody has to find
            // out by being refused one.
            Assert.Equal(0, result.ExitCode);

            string[] options = ["--delay", "--time-limit"];

            foreach (string option in options)
            {
                string line = Assert.Single(result.Output.Split('\n'), candidate => candidate.TrimStart().StartsWith($"{option} ", StringComparison.Ordinal));
                Assert.Contains("30s", line, StringComparison.Ordinal);
                Assert.Contains("HH:MM:SS", line, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// A host whose runs are bounded by a limit that records the duration it was given, with
        /// the stub tester that finishes well inside any of them.
        /// </summary>
        private static (CommandLineTestHost Host, RecordingRunTimeLimit Limit) LimitRecordingHost()
        {
            var limit = new RecordingRunTimeLimit();
            var services = StubServices();
            services.AddSingleton<IRunTimeLimit>(limit);
            return (GetCommandLineTestHost(services), limit);
        }

        /// <summary>
        /// A wait between runs that takes no time, and notes how long it was asked to wait.
        /// </summary>
        private sealed class RecordingWaiter : IWaiter
        {
            public List<TimeSpan> Waited { get; } = [];

            public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
            {
                Waited.Add(delay);
                return Task.CompletedTask;
            }
        }
    }
}
