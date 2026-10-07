using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

/// <summary>
/// CLI behaviour for choosing an output format: <c>--minimal</c>, <c>--csv</c> and <c>--json</c>
/// are mutually exclusive peers, the retired <c>--verbosity</c> switch is rejected outright, and
/// the default format carries no diagnostic prose.
/// </summary>
public sealed partial class NetPaceConsoleTests
{
    public sealed class OutputFormat
    {
        private static CommandLineTestHost HostWith()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        [Fact]
        public async Task Minimal_Produces_The_Compact_Single_Line_Result()
        {
            // Given
            var host = HostWith();

            // When
            var result = await host.RunAsync(["--minimal"]);

            // Then the whole output is one result line.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Minimal_Composes_With_The_Result_Shaping_Options()
        {
            // Given
            var host = HostWith();

            // When the compact format is combined with timestamp, unit and scale selection.
            var result = await host.RunAsync(["--minimal", "-t", "--unit", "BytesPerSecond", "--unit-scale", "Kilo", "--unit-system", "IEC"]);

            // Then the single result line carries the timestamp and the chosen units.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [InlineData("")]
        [InlineData("--minimal")]
        [InlineData("--verbosity Debug")]
        public async Task No_Invocation_Emits_Bytes_And_Duration_Prose(string commandLine)
        {
            // The bytes-and-duration lines the retired --verbosity Debug added are gone, and no
            // switch brings them back - including the invocation that used to ask for them.

            // Given
            var host = HostWith();

            // When
            var result = await host.RunAsync(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            // Then
            Assert.DoesNotContain("downloaded in", result.Output);
            Assert.DoesNotContain("uploaded in", result.Output);
        }

        [Theory]
        [InlineData("--verbosity")]
        [InlineData("--verbosity Minimal")]
        [InlineData("--verbosity Normal")]
        [InlineData("--verbosity Debug")]
        [InlineData("--verbosity NotAValue")]
        public async Task Verbosity_Is_Rejected_With_The_Same_Error_Whatever_Its_Value(string commandLine)
        {
            // Given
            var host = HostWith();

            // When
            var result = await host.RunAsync(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            // Then the error says the switch is gone and points at --help, and names no
            // replacement - --minimal replaces only one of the three former values.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("--verbosity has been removed, see --help.", result.Output);
            Assert.DoesNotContain("--minimal", result.Output);
        }

        [Theory]
        [InlineData("--csv --json", "--csv, --json")]
        [InlineData("--csv --minimal", "--csv, --minimal")]
        [InlineData("--json --minimal", "--json, --minimal")]
        [InlineData("--csv --json-pretty", "--csv, --json-pretty")]
        [InlineData("--json-pretty --minimal", "--json-pretty, --minimal")]
        [InlineData("--csv --json --minimal", "--csv, --json, --minimal")]
        public async Task More_Than_One_Output_Format_Is_Rejected_Naming_The_Conflict(string commandLine, string conflict)
        {
            // Given
            var host = HostWith();

            // When
            var result = await host.RunAsync(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            // Then the error identifies which switches conflicted rather than silently picking one.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"Only one output format may be specified: {conflict}.", result.Output);
        }

        [Fact]
        public async Task Json_And_JsonPretty_Together_Remain_Accepted()
        {
            // --json-pretty shapes the same format rather than selecting a second one.

            // Given
            var host = HostWith();

            // When
            var result = await host.RunAsync(["--json", "--json-pretty"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("Only one output format", result.Output);
        }
    }
}
