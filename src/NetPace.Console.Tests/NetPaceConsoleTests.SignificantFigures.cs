using Microsoft.Extensions.DependencyInjection;
using NetPace.Core;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// The three significant figures a speed is written to, read in each of the formats a person
    /// reads: the default table, the one-line <c>--minimal</c> and the line-per-figure
    /// <c>--simple</c>.
    /// </summary>
    /// <remarks>
    /// All three take their figure from the same library extension, so the test that matters is
    /// that they still agree: a format formatting a speed on its own is how two of them come to
    /// disagree, and it would not show in a snapshot of any one of them.
    /// </remarks>
    public sealed class SignificantFigures
    {
        private static ServiceCollection Services(long bytesProcessed, long elapsedMilliseconds)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(bytesProcessed, elapsedMilliseconds),
                UploadFactory = _ => Transfer(bytesProcessed, elapsedMilliseconds)
            });
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        private static SpeedTestResult Transfer(long bytesProcessed, long elapsedMilliseconds) =>
            new()
            {
                BytesProcessed = bytesProcessed,
                ElapsedMilliseconds = elapsedMilliseconds,
                RequestsSucceeded = 10,
                RequestsFailed = 0,
                Latency = null
            };

        // 8 Mbps, which the band below 10 writes to two decimal places.
        [InlineData(1_000_000L, 1000L, "8.00 Mbps")]
        // 18.666... Mbps, which the band from 10 writes to one.
        [InlineData(7_000_000L, 3000L, "18.7 Mbps")]
        // 245.4 Mbps, which the band from 100 writes whole.
        [InlineData(30_675_000L, 1000L, "245 Mbps")]
        [Theory]
        public async Task A_Speed_Reads_The_Same_Three_Significant_Figures_In_Default_Minimal_And_Simple_Output(long bytesProcessed, long elapsedMilliseconds, string expected)
        {
            // SCENARIO: Speeds are written to three significant figures

            // Given the same measured transfer written in each of the three formats a person reads.
            var byDefault = await new CommandLineTestHost(Services(bytesProcessed, elapsedMilliseconds), CommandLineTestHost.InteractiveTerminalWidth, interactive: true).RunAsync([]);
            var minimal = await GetCommandLineTestHost(Services(bytesProcessed, elapsedMilliseconds)).RunAsync(["--minimal"]);
            var simple = await GetCommandLineTestHost(Services(bytesProcessed, elapsedMilliseconds)).RunAsync(["--simple"]);

            // Then each one reports the figure to three significant figures, and all three report
            // the same text, so no format is rounding the speed on its own.
            Assert.Equal(0, byDefault.ExitCode);
            Assert.Equal(0, minimal.ExitCode);
            Assert.Equal(0, simple.ExitCode);

            Assert.Contains(expected, byDefault.Output, StringComparison.Ordinal);
            Assert.Contains($"Download: {expected}", minimal.Output, StringComparison.Ordinal);
            Assert.Contains($"Download: {expected}", simple.Output, StringComparison.Ordinal);
        }
    }
}
