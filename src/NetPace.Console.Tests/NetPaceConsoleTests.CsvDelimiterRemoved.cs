namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--csv-delimiter</c> switch is gone. CSV output is always comma-separated, so a script
/// reading a NetPace log never has to know which separator a given run chose.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class CsvDelimiterRemoved
    {
        /// <summary>
        /// A host whose speed tester records the one call a run makes before measuring — server
        /// discovery — so a test can assert the run never got that far. Every later delegate is left
        /// unconfigured and throws, so a run that got past discovery could not pass quietly either.
        /// </summary>
        private static (CommandLineTestHost host, List<string> serviceCalls) HostRecordingMeasurements()
        {
            var serviceCalls = new List<string>();

            var speedTester = new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    serviceCalls.Add(nameof(ISpeedTestService.GetServersAsync));
                    return Task.FromResult<IServer[]>([]);
                }
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();

            return (new CommandLineTestHost(services), serviceCalls);
        }

        [Theory]
        [InlineData(";")]
        [InlineData("\t")]
        [InlineData(",")]
        public async Task CsvDelimiterOption_WhateverTheValue_IsRejectedWithoutMeasuring(string delimiter)
        {
            // SCENARIO: CSV delimiter option is rejected

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When the retired option is passed, with a value that used to be accepted.
            var result = await host.RunAsync(["--csv", "--csv-delimiter", delimiter]);

            // Then nothing was even discovered, the exit code reports the usage error, and the
            // message names the option so the user is not left with a bare failure.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--csv-delimiter", result.Output);
        }

        [Fact]
        public async Task CsvDelimiterOption_WithNoValue_IsRejectedWithoutMeasuring()
        {
            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When the option is passed bare, which a parse error must also catch.
            var result = await host.RunAsync(["--csv", "--csv-delimiter"]);

            // Then
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("--csv-delimiter", result.Output);
        }

        [Fact]
        public async Task Help_NoLongerAdvertisesTheCsvDelimiterOption()
        {
            // Given
            var (host, _) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--csv-delimiter", result.Output);
        }
    }
}
