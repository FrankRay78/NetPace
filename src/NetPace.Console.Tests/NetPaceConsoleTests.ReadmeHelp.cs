using Microsoft.Extensions.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class ReadmeHelp
    {
        [Fact]
        public async Task Should_Match_The_Help_Block_Reproduced_In_Readme()
        {
            // SCENARIO: README help block matches the real help output
            // SCENARIO: README help drift fails the test suite

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--help"]);

            // Then the README holds the whole help output as one fenced block.

            // Compared with LF endings and trailing whitespace trimmed, because the help grid pads
            // its last column. Paste from Expectations/NetPaceConsoleTests.Should_Display_Help.verified.txt.
            static string Normalise(string text) =>
                string.Join("\n", text.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()));

            var help = Normalise(result.Output).TrimEnd();
            var readme = Normalise(System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md")));

            Assert.Contains($"```txt\n$ netpace --help\n{help}\n```", readme);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
