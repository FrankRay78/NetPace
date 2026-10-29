namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--csv-delimiter</c> switch is gone. CSV output is always comma-separated, so a script
/// reading a NetPace log never has to know which separator a given run chose.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class CsvDelimiterRemoved
    {
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

            // Then nothing was measured, and the snapshot carries what the user is told. This is the
            // suite's only snapshot of an unrecognised option, so it is what pins that rendering.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            await Verify(result.Output);
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
