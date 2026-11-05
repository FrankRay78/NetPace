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
            Assert.Equal(0, result.ExitCode);
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
            Assert.Contains("Performs the speed test in a loop.", report);
            Assert.Contains("Performs the speed test on continuous loop.", report);
            Assert.Contains("Stop speed testing after this many times.", report);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Should_Name_Only_The_Added_Or_Removed_Line_When_An_Option_Changes_Mid_Block(
            bool optionAddedToHelp)
        {
            // Given help with an option added or removed in the middle, and a README block that
            // was not updated to match.
            string[] withoutOption =
            [
                "",
                "OPTIONS:",
                "        --first                       First unchanged option.",
                "        --second                      Second unchanged option.",
                "        --third                       Third unchanged option."
            ];
            var withOption = withoutOption
                .Take(3)
                .Append("        --middle                      The option that changed.")
                .Concat(withoutOption.Skip(3))
                .ToArray();

            var readmeLines = optionAddedToHelp ? withoutOption : withOption;
            var helpLines = optionAddedToHelp ? withOption : withoutOption;

            var readme = ReadmeHelpBlock.Extract(
                $"```txt\n{ReadmeHelpBlock.PromptLine}\n{string.Join("\n", readmeLines)}\n```\n");
            var help = ReadmeHelpBlock.Normalise(string.Join("\n", helpLines));

            // When
            var report = ReadmeHelpBlock.Compare(readme, help);

            // Then the changed option is named, and the lines around it, which still match, are not.
            Assert.NotNull(report);
            Assert.Contains("The option that changed.", report);
            Assert.DoesNotContain("First unchanged option.", report);
            Assert.DoesNotContain("Second unchanged option.", report);
            Assert.DoesNotContain("Third unchanged option.", report);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void Should_Find_The_Readme_Block_When_A_Fence_Or_Prompt_Line_Has_Trailing_Whitespace(
            int lineWithTrailingSpace)
        {
            // Given a README block whose opening fence, prompt line or closing fence carries a
            // trailing space — invisible, and rendered identically — followed by another fenced
            // block the help block must not run on into.
            string[] blockEdges = ["```txt", ReadmeHelpBlock.PromptLine, "```"];
            blockEdges[lineWithTrailingSpace] += " ";

            var readme = ReadmeHelpBlock.Extract(
                $"""
                Preamble.

                {blockEdges[0]}
                {blockEdges[1]}

                OPTIONS:
                        --loop                        Performs the speed test on continuous loop.
                {blockEdges[2]}

                ## Developer Use

                ```bash
                dotnet add package NetPace.Core
                ```
                """);

            var help = ReadmeHelpBlock.Normalise(
                """

                OPTIONS:
                        --loop                        Performs the speed test on continuous loop.
                """);

            // When
            var report = ReadmeHelpBlock.Compare(readme, help);

            // Then the block is found, ends where it should, and agrees with the help output.
            Assert.True(report is null, report);
        }

        [Theory]
        [InlineData("\n", "")]
        [InlineData("\r\n", "")]
        [InlineData("\r", "")]
        [InlineData("\n", "\uFEFF")]
        public void Should_Agree_With_The_Readme_Block_Whatever_Line_Endings_The_Help_Output_Uses(
            string lineEnding, string byteOrderMark)
        {
            // Given a README block stored with the LF endings every checkout gets, and the same
            // help output as a platform's console hands it over — CRLF or CR, byte-order-marked
            // or not.
            string[] helpLines =
            [
                "",
                "OPTIONS:",
                "        --loop                        Performs the speed test on continuous loop."
            ];

            var readme = ReadmeHelpBlock.Extract(
                $"```txt\n{ReadmeHelpBlock.PromptLine}\n{string.Join("\n", helpLines)}\n```\n");
            var help = ReadmeHelpBlock.Normalise(
                byteOrderMark + string.Join(lineEnding, helpLines) + lineEnding);

            // When
            var report = ReadmeHelpBlock.Compare(readme, help);

            // Then the check reaches the same verdict, so it gives the same result on every
            // platform.
            Assert.True(report is null, report);
        }
    }
}
