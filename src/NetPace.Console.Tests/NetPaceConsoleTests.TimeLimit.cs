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

            // The snapshot is the whole output, so a result line appearing would fail it, and it
            // holds the message that has to say what a valid value looks like.
            await Verify(result.Output).UseTextForParameters(valueName);
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
    }
}
