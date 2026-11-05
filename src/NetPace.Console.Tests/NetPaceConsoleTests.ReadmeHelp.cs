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

            // Then the README holds the whole help output as one fenced block. Trailing whitespace
            // is trimmed because the help grid pads its last column; .gitattributes checks the
            // README out with LF endings everywhere. On failure, the text to paste into the README
            // is in Expectations/NetPaceConsoleTests.Should_Display_Help.verified.txt.
            var help = string.Join("\n",
                result.Output.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd())).TrimEnd();
            var readme = System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "README.md"));

            Assert.Contains($"```txt\nC:\\>netpace.exe --help\n{help}\n```", readme);
            Assert.Equal(0, result.ExitCode);
        }
    }
}
