namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--datetimeformat</c> switch is gone. Each output mode now has one fixed timestamp format,
/// so a script reading CSV or JSON no longer has to know which format a run chose, and a value that
/// contained the CSV delimiter can no longer split the timestamp across two fields.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class DateTimeFormatRemoved
    {
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

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            return (new CommandLineTestHost(services), measurementCalls);
        }

        [Theory]
        [InlineData("yyyy-MM-dd HH:mm:ss")]
        [InlineData("dd/MM/yyyy")]
        [InlineData("Q")]
        [InlineData("")]
        public async Task DateTimeFormatOption_WhateverTheValue_IsRejectedWithoutMeasuring(string format)
        {
            // SCENARIO: Datetimeformat option is no longer accepted

            // Given
            var (host, measurementCalls) = HostRecordingMeasurements();

            // When the retired option is passed, with a value that used to be accepted or not.
            var result = await host.RunAsync(["--minimal", "-t", "--datetimeformat", format]);

            // Then nothing was measured and the exit code reports the usage error.
            Assert.Empty(measurementCalls);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public async Task DateTimeFormatOption_WithNoValue_IsRejectedWithoutMeasuring()
        {
            // Given
            var (host, measurementCalls) = HostRecordingMeasurements();

            // When the option is passed bare, which a parse error must also catch.
            var result = await host.RunAsync(["--minimal", "-t", "--datetimeformat"]);

            // Then
            Assert.Empty(measurementCalls);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public async Task Help_NoLongerAdvertisesTheDateTimeFormatOption()
        {
            // Given
            var (host, _) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--datetimeformat", result.Output);
        }
    }
}
