namespace NetPace.Console.Tests;

/// <summary>
/// The <c>--server</c> switch is now <c>--server-url</c>, which says what it takes and leaves room
/// for naming a server another way. The old name stays registered only to tell a script what to
/// change.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class ServerOptionRenamed
    {
        [Theory]
        [InlineData("http://test1.com")]
        [InlineData("http://localhost:18080/speedtest/upload.php")]
        public async Task ServerOption_WithAUrl_IsRejectedWithoutMeasuringAndNamesServerUrl(string url)
        {
            // SCENARIO: The old option name says what replaced it

            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When the old option name is passed with a URL that the new name accepts.
            var result = await host.RunAsync(["--server", url]);

            // Then neither server discovery nor the latency probe a named server goes straight to
            // was reached, the exit code reports the usage error, and the message names the option
            // to use instead. Its wording is not asserted: that is a message for a person to read.
            Assert.Empty(serviceCalls);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("--server-url", result.ErrorOutput);
        }

        [Fact]
        public async Task ServerOption_WithNoValue_IsRejectedWithoutMeasuringAndNamesServerUrl()
        {
            // Given
            var (host, serviceCalls) = HostRecordingMeasurements();

            // When the option is passed bare, which must land on the same message rather than a
            // built-in missing-argument parse failure.
            var result = await host.RunAsync(["--server"]);

            // Then
            Assert.Empty(serviceCalls);
            Assert.Equal(2, result.ExitCode);
            Assert.Contains("--server-url", result.ErrorOutput);
        }

        [Fact]
        public async Task Help_NoLongerAdvertisesTheServerOption()
        {
            // Given
            var (host, _) = HostRecordingMeasurements();

            // When
            var result = await host.RunAsync(["--help"]);

            // Then no --server survives but the --server-url that replaced it and --server-file.
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotMatch("--server(?!-url|-file)", result.Output);
        }
    }
}
