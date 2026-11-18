using NetPace.Console.Tests.Theming;
using NetPace.Console.Theming;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// The logo both help screens open with: pixel lettering drawn in the theme's two colours
    /// wherever the console can draw it, and today's ASCII lettering wherever it cannot.
    /// </summary>
    /// <remarks>
    /// The snapshots here hold the help screen down to the line above DESCRIPTION, which is the
    /// logo and the blank lines around it. The whole help screen is snapshotted elsewhere; reading
    /// only the opening here keeps a colour change to the lettering legible in a diff instead of
    /// being one line of a long screen.
    /// </remarks>
    public sealed class HelpLogo
    {
        /// <summary>
        /// The pixel logo, written out here rather than read from the production code, so changing
        /// the lettering has to be accepted here as well as there. The first nine columns of each
        /// row spell NET and the rest spell PACE.
        /// </summary>
        private static readonly string[] PixelRows =
        [
            "▙ ▌▛▀▘▀▛▘▛▀▖▞▀▖▞▀▘▛▀▘",
            "▌▚▌▛▀  ▌ ▛▀ ▛▀▌▌  ▛▀",
            "▘ ▘▀▀▘ ▘ ▘  ▘ ▘▝▀▘▀▀▘"
        ];

        /// <summary>The help screens the logo opens, both drawn by the same renderer.</summary>
        private static readonly string[] HelpScreens = ["--help", "servers --help"];

        private static ServiceCollection Services(IThemeProvider themeProvider)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            services.AddSingleton(themeProvider);
            return services;
        }

        /// <summary>
        /// A run whose environment holds the variables given here and nothing else, so what the
        /// user set reaches the run without the suite's own environment being touched.
        /// </summary>
        private static IThemeProvider FromEnvironment(params (string Name, string Value)[] variables) =>
            new EnvironmentThemeProvider(name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault());

        /// <summary>The screen a theory row names, as a snapshot file reads it.</summary>
        private static string Slug(string screen) => string.Join('-', screen.Split(' ').Select(word => word.TrimStart('-')));

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
            // SCENARIO: The help logo is drawn in the theme's two colours

            // Given
            var host = new CommandLineTestHost(
                Services(FromEnvironment(("NETPACE_THEME", theme))),
                CommandLineTestHost.RedirectedWidth,
                recordColour: true);

            // When
            var result = await host.RunAsync(screen.Split(' '));

            // Then the lettering is drawn, NET in the theme's first colour and PACE in its second.
            Assert.Equal(0, result.ExitCode);
            Assert.All(PixelRows, row => Assert.Contains(row, AnsiSnapshot.WithoutStyling(result.Output), StringComparison.Ordinal));
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
            if (theme is not null) variables.Add(("NETPACE_THEME", theme));
            if (noColour is not null) variables.Add(("NO_COLOR", noColour));

            var host = new CommandLineTestHost(
                Services(FromEnvironment([.. variables])),
                CommandLineTestHost.RedirectedWidth,
                recordColour: true);

            // When
            var result = await host.RunAsync(screen.Split(' '));

            // Then the same lettering is drawn, and nothing anywhere on the screen is coloured.
            Assert.Equal(0, result.ExitCode);
            Assert.All(PixelRows, row => Assert.Contains(row, result.Output, StringComparison.Ordinal));
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
        }

        [InlineData("--help")]
        [InlineData("servers --help")]
        [Theory]
        public async Task AConsoleThatCannotDrawUnicode_OpensEachHelpScreenWithTheAsciiLogo(string screen)
        {
            // SCENARIO: A console without Unicode keeps the ASCII logo

            // Given, When the same screen is drawn on a console that can draw Unicode and on one
            // that cannot
            string canDraw = await Draw(screen, unicode: true);
            string cannotDraw = await Draw(screen, unicode: false);

            // Then the console that cannot draw the pixel lettering is given other lettering
            // instead, rather than the characters it cannot draw.
            Assert.All(PixelRows, row => Assert.DoesNotContain(row, cannotDraw, StringComparison.Ordinal));
            Assert.NotEqual(canDraw, cannotDraw);
            await Verify(cannotDraw).UseTextForParameters(Slug(screen));

            async Task<string> Draw(string arguments, bool unicode)
            {
                var host = new CommandLineTestHost(
                    Services(FromEnvironment()),
                    CommandLineTestHost.RedirectedWidth,
                    unicode: unicode);

                var result = await host.RunAsync(arguments.Split(' '));

                Assert.Equal(0, result.ExitCode);
                return Opening(result.Output);
            }
        }
    }
}
