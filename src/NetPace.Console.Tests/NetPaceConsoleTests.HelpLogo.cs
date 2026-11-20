using NetPace.Console.Tests.Theming;
using NetPace.Console.Theming;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// The logo both help screens open with: ASCII lettering drawn in the theme's two colours,
    /// the same on every console.
    /// </summary>
    /// <remarks>
    /// The snapshots here hold the help screen down to the line above DESCRIPTION, which is the
    /// logo and the blank lines around it. The whole help screen is snapshotted elsewhere; reading
    /// only the opening here keeps a colour change to the lettering legible in a diff instead of
    /// being one line of a long screen.
    /// </remarks>
    public sealed class HelpLogo
    {
        /// <summary>The help screens the logo opens, both drawn by the same renderer.</summary>
        private static readonly string[] HelpScreens = ["--help", "servers --help"];

        /// <summary>
        /// Both help screens, one theory row each, so a screen added to <see cref="HelpScreens"/>
        /// reaches every theory here rather than only the ones that read the list.
        /// </summary>
        public static TheoryData<string> HelpScreenRows()
        {
            var rows = new TheoryData<string>();
            foreach (string screen in HelpScreens)
            {
                rows.Add(screen);
            }
            return rows;
        }

        /// <summary>The standing stub registrations, with the theme the run is drawn in.</summary>
        private static ServiceCollection Services(IThemeProvider themeProvider)
        {
            var services = StubServices();
            services.AddSingleton(themeProvider);
            return services;
        }

        /// <summary>
        /// What the help screen opens with: everything above the DESCRIPTION heading.
        /// </summary>
        private static string Opening(string output)
        {
            int description = output.IndexOf("DESCRIPTION:", StringComparison.Ordinal);
            Assert.True(description >= 0, "the help screen carries no DESCRIPTION heading to read the logo above");

            return output[..(output.LastIndexOf('\n', description) + 1)];
        }

        /// <summary>Every theme that draws in colour, against both help screens.</summary>
        public static TheoryData<string, string> ColouredThemesAndHelpScreens()
        {
            var rows = new TheoryData<string, string>();
            foreach (var theme in ThemeRows.InColour)
            {
                foreach (string screen in HelpScreens)
                {
                    rows.Add(theme.Name, screen);
                }
            }
            return rows;
        }

        [MemberData(nameof(ColouredThemesAndHelpScreens))]
        [Theory]
        public async Task EachHelpScreen_UnderAThemeThatDrawsInColour_OpensWithTheLogoInThatThemesTwoColours(string theme, string screen)
        {
            // SCENARIO: Logo halves keep the theme's two colours

            // Given
            var host = new CommandLineTestHost(
                Services(FromEnvironment(("NETPACE_THEME", theme))),
                CommandLineTestHost.RedirectedWidth,
                recordColour: true);

            // When
            var result = await host.RunAsync(screen.Split(' '));

            // Then the lettering is drawn, NET in the theme's first colour and PACE in its second.
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(Opening(result.Output))).UseTextForParameters($"{theme}_{Slug(screen)}");
        }

        /// <summary>
        /// Every way a user asks for no colour, against both help screens: the plain theme, and
        /// NO_COLOR both over a coloured theme and on its own.
        /// </summary>
        public static TheoryData<string?, string?, string> NoColourAndHelpScreens()
        {
            (string? Theme, string? NoColour)[] asked = [("plain", null), ("halloween", "1"), (null, "1")];

            var rows = new TheoryData<string?, string?, string>();
            foreach (var (theme, noColour) in asked)
            {
                foreach (string screen in HelpScreens)
                {
                    rows.Add(theme, noColour, screen);
                }
            }
            return rows;
        }

        [MemberData(nameof(NoColourAndHelpScreens))]
        [Theory]
        public async Task EachHelpScreen_WhereNoColourIsDrawn_OpensWithTheSameLogoUncoloured(string? theme, string? noColour, string screen)
        {
            // SCENARIO: The help logo reads without colour

            // Given an environment that asks for no colour, by the plain theme or by NO_COLOR
            var variables = new List<(string Name, string Value)>();
            if (theme is not null)
            {
                variables.Add(("NETPACE_THEME", theme));
            }
            if (noColour is not null)
            {
                variables.Add(("NO_COLOR", noColour));
            }

            var host = new CommandLineTestHost(
                Services(FromEnvironment([.. variables])),
                CommandLineTestHost.RedirectedWidth,
                recordColour: true);

            // When
            var result = await host.RunAsync(screen.Split(' '));

            // Then the screen opens with the same lettering, and nothing anywhere on it is coloured.
            Assert.Equal(0, result.ExitCode);
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
            await Verify(Opening(result.Output))
                .UseTextForParameters(string.Join('_', variables.Select(variable => variable.Name == "NETPACE_THEME" ? variable.Value : variable.Name).Append(Slug(screen))));
        }

        [MemberData(nameof(HelpScreenRows))]
        [Theory]
        public async Task EachHelpScreen_OpensWithTheLetteringDrawnInAsciiAlone(string screen)
        {
            // SCENARIO: Help opens with the ASCII logo

            // Given
            var host = new CommandLineTestHost(
                Services(FromEnvironment()),
                CommandLineTestHost.RedirectedWidth);

            // When
            var result = await host.RunAsync(screen.Split(' '));

            // Then the screen opens with the lettering, and every character it is made of is one
            // any monospace font draws at the same width: nothing above the ASCII range.
            Assert.Equal(0, result.ExitCode);
            string opening = Opening(result.Output);
            char[] outsideAscii = [.. opening.Where(character => character > '\u007f')];
            Assert.Empty(outsideAscii);
            await Verify(opening).UseTextForParameters(Slug(screen));
        }

        [MemberData(nameof(HelpScreenRows))]
        [Theory]
        public async Task AConsoleThatCannotCarryUnicode_OpensEachHelpScreenWithTheSameLogo(string screen)
        {
            // SCENARIO: Every console gets the same logo

            // Given, When the same screen is drawn on a console that can carry Unicode and on one
            // that cannot
            string canCarry = await Draw(screen, unicode: true);
            string cannotCarry = await Draw(screen, unicode: false);

            // Then both are given the same lettering: there is nothing in it an output encoding
            // can fail to carry, so there is no second logo to fall back to.
            Assert.Equal(canCarry, cannotCarry);
            await Verify(AnsiSnapshot.Readable(cannotCarry)).UseTextForParameters(Slug(screen));

            async Task<string> Draw(string arguments, bool unicode)
            {
                var host = new CommandLineTestHost(
                    Services(FromEnvironment()),
                    CommandLineTestHost.RedirectedWidth,
                    recordColour: true,
                    unicode: unicode);

                var result = await host.RunAsync(arguments.Split(' '));

                Assert.Equal(0, result.ExitCode);
                return Opening(result.Output);
            }
        }
    }
}
