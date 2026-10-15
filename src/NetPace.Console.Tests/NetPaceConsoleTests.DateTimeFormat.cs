namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for <c>--datetimeformat</c>: a value that cannot format a date is a usage error,
/// reported before any measurement is attempted and in every output mode, while valid formats —
/// including the default — run and render the timestamp as before.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class DateTimeFormat
    {
        /// <summary>
        /// A speed tester that records every measurement it is asked for, so a test can assert it
        /// was asked for none. Server discovery is the first thing any run needs, and the
        /// unconfigured delegates beyond it throw, so a run that gets that far cannot pass quietly.
        /// </summary>
        private static (IServiceCollection services, List<string> measurementCalls) ServicesRecordingMeasurements()
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

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            return (services, measurementCalls);
        }

        /// <summary>
        /// A speed tester that measures normally, against a clock fixed at 1980-01-01 10:05:00.
        /// </summary>
        private static IServiceCollection ServicesThatMeasure()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            return services;
        }

        private static CommandLineTestHost HostWith(IServiceCollection services)
        {
            return new CommandLineTestHost(services);
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
            var (services, measurementCalls) = ServicesRecordingMeasurements();
            var host = HostWith(services);

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
            var (services, measurementCalls) = ServicesRecordingMeasurements();
            var host = HostWith(services);

            // When the run would never have used the format, because no timestamp was asked for.
            var result = await host.RunAsync([.. ArgsFrom(commandLine), "--datetimeformat", "Q"]);

            // Then it is rejected just the same, rather than waiting to fail once --timestamp is added.
            Assert.Empty(measurementCalls);
            Assert.Contains("--datetimeformat", result.Output);
            Assert.Contains("'Q'", result.Output);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Theory]
        [InlineData("%")]
        [InlineData("Q")]
        [InlineData("yyyy-MM-dd \\")]
        public async Task InvalidDateTimeFormat_WhateverTheValue_IsRejectedWithoutMeasuring(string format)
        {
            // Given
            var (services, measurementCalls) = ServicesRecordingMeasurements();
            var host = HostWith(services);

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
            var host = HostWith(ServicesThatMeasure());

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
            var host = HostWith(ServicesThatMeasure());

            // When no format is given at all.
            var result = await host.RunAsync(["--minimal", "-t"]);

            // Then the default format still renders.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("1980-01-01 10:05:00", result.Output);
        }
    }
}
