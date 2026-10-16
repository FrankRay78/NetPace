namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for <c>--datetimeformat</c>: a value that cannot format a date is a usage error,
/// reported before any measurement is attempted and in every output format, while valid formats —
/// including the default — run and render the timestamp as before.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class DateTimeFormat
    {
        /// <summary>
        /// Hosts the CLI against the given speed tester, a clock fixed at 1980-01-01 10:05:00, and
        /// no inter-test delay.
        /// </summary>
        private static CommandLineTestHost HostWith(ISpeedTestService speedTester)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        /// <summary>
        /// A host whose speed tester records the one call a run makes before measuring — server
        /// discovery — so a test can assert the run never got that far. Every measurement delegate
        /// is left unconfigured and throws, so a run that proceeds cannot pass quietly.
        /// </summary>
        private static (CommandLineTestHost host, List<string> measurementCalls) HostRecordingMeasurements()
        {
            var measurementCalls = new List<string>();

            var speedTester = new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    measurementCalls.Add(nameof(ISpeedTestService.GetServersAsync));
                    return Task.FromResult<IServer[]>([]);
                }
            };

            return (HostWith(speedTester), measurementCalls);
        }

        private static string[] ArgsFrom(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        [Theory]
        [InlineData("-t")]
        [InlineData("--minimal -t")]
        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        public async Task InvalidDateTimeFormat_WithTimestampShown_IsRejectedWithoutMeasuring(string commandLine)
        {
            // SCENARIO: Invalid datetime format is rejected before testing

            // Given
            var (host, measurementCalls) = HostRecordingMeasurements();

            // When a format that cannot format a date is passed to a run that would print a timestamp.
            var result = await host.RunAsync([.. ArgsFrom(commandLine), "--datetimeformat", "Q"]);

            // Then nothing was measured, the option is named, and the exit code reports the usage error.
            Assert.Empty(measurementCalls);
            Assert.Contains("--datetimeformat", result.Output);
            Assert.Contains("'Q'", result.Output);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData("--minimal")]
        public async Task InvalidDateTimeFormat_WithNoTimestampShown_IsRejectedWithoutMeasuring(string commandLine)
        {
            // SCENARIO: Invalid datetime format is rejected even when no timestamp is shown

            // Given
            var (host, measurementCalls) = HostRecordingMeasurements();

            // When the run would never have used the format, because no timestamp was asked for.
            var result = await host.RunAsync([.. ArgsFrom(commandLine), "--datetimeformat", "Q"]);

            // Then it is rejected just the same, rather than waiting to fail once --timestamp is added.
            Assert.Empty(measurementCalls);
            Assert.Contains("--datetimeformat", result.Output);
            Assert.Contains("'Q'", result.Output);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("%")]
        [InlineData("yyyy-MM-dd \\")]
        public async Task InvalidDateTimeFormat_WhateverTheValue_IsRejectedWithoutMeasuring(string format)
        {
            // The theories above vary the output mode around one bad value; this one varies the
            // value, so a format spoiled only by a stray escape is rejected as squarely as a
            // single unusable letter.

            // Given
            var (host, measurementCalls) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--minimal", "-t", "--datetimeformat", format]);

            // Then
            Assert.Empty(measurementCalls);
            Assert.Contains("--datetimeformat", result.Output);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public async Task ValidCustomDateTimeFormat_RunsTheTestAndRendersTheTimestamp()
        {
            // SCENARIO: Valid custom datetime format is still accepted

            // Given
            var host = HostWith(new SpeedTestStub());

            // When
            var result = await host.RunAsync(["--minimal", "-t", "--datetimeformat", "yyyyMMdd_HHmm"]);

            // Then the test ran and the timestamp carries the requested format.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("19800101_1005", result.Output);
        }

        [Fact]
        public async Task DefaultDateTimeFormat_RunsTheTestAndRendersTheTimestamp()
        {
            // Given
            var host = HostWith(new SpeedTestStub());

            // When no format is given at all.
            var result = await host.RunAsync(["--minimal", "-t"]);

            // Then the default format still renders.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("1980-01-01 10:05:00", result.Output);
        }
    }
}
