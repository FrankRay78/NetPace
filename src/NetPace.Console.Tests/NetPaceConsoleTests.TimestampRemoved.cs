namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--timestamp</c> switch is gone. Every output format now either always carries the time or
/// never does: <c>--minimal</c>, CSV and JSON always do, while <c>--simple</c> and the default
/// output never do, so there is nothing left for a switch to turn on.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class TimestampRemoved
    {
        [Theory]
        [InlineData("--timestamp")]
        [InlineData("-t")]
        public async Task TimestampOption_IsRejectedAsAnUnrecognisedOption(string option)
        {
            // SCENARIO: The timestamp switch says it was removed

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When the removed switch is passed
            var result = await host.RunAsync([option]);

            // Then nothing was measured, the exit code reports the usage error, and the message is
            // the parser's own unrecognised-option one: nothing names the switch as removed or says
            // where the time went, because NetPace keeps no backward-compatibility notes while it is
            // below 1.0.0.
            Assert.Empty(serviceCalls);
            Assert.NotEqual(0, result.ExitCode);
            Assert.DoesNotContain("removed", result.Output);
            await Verify(result.Output).UseParameters(option);
        }

        [Fact]
        public async Task Help_NoLongerAdvertisesTheTimestampOption()
        {
            // Given
            var (host, _) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("--timestamp", result.Output);
        }

        [Fact]
        public async Task DefaultOutput_CarriesNoTimestamp()
        {
            // Given a run at a terminal, which is where default output draws its own block
            var host = GetInteractiveCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync([]);

            // Then the stub clock's reading is nowhere in it.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("1980-01-01", result.Output);
        }

        [Theory]
        [InlineData("--csv")]
        [InlineData("--json")]
        public async Task MachineReadableOutput_StillCarriesItsTimestamp(string formatSwitch)
        {
            // Given a run in a format a program reads

            // When
            var result = await GetCommandLineTestHost(StubServices()).RunAsync([formatSwitch]);

            // Then the instant is still there, offset and all: removing the switch took nothing
            // away from the formats that always carried the time.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("1980-01-01T10:05:00+01:00", result.Output);
        }
    }
}
