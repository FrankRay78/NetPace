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

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When the help the README reproduces is compared with the help the command prints.
            var result = await host.RunAsync(["--help"]);
            var readme = ReadmeHelpBlock.Extract(ReadmeHelpBlock.ReadReadme());
            var help = ReadmeHelpBlock.Normalise(result.Output);

            // Then. Asserted as a message rather than with Assert.Null, which truncates the
            // report and so hides the very lines a contributor needs to see.
            var report = ReadmeHelpBlock.Compare(readme, help);
            Assert.True(report is null, report);
        }

        [Fact]
        public void Should_Name_The_Differing_Lines_When_Readme_Help_Has_Drifted()
        {
            // SCENARIO: README help drift fails the test suite

            // Given a README whose help block has been left behind by a reworded option.
            var readme = ReadmeHelpBlock.Extract(
                $"""
                Preamble.

                ```txt
                {ReadmeHelpBlock.PromptLine}

                OPTIONS:
                        --loop                        Performs the speed test in a loop.
                ```

                Epilogue.
                """);

            var help = ReadmeHelpBlock.Normalise(
                """

                OPTIONS:
                        --loop                        Performs the speed test on continuous loop.
                        --count                       Stop speed testing after this many times.
                """);

            // When
            var report = ReadmeHelpBlock.Compare(readme, help);

            // Then the reworded line and the line missing altogether are both named.
            Assert.NotNull(report);
            Assert.Contains("block line 3", report);
            Assert.Contains("Performs the speed test in a loop.", report);
            Assert.Contains("Performs the speed test on continuous loop.", report);
            Assert.Contains("block line 4", report);
            Assert.Contains("Stop speed testing after this many times.", report);
            Assert.Contains("(no line)", report);
        }
    }
}
