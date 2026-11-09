using NetPace.Console.Tests.Theming;
using NetPace.Console.Theming;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// What a chosen theme changes about the screens a person reads, and what it must leave alone.
    /// </summary>
    /// <remarks>
    /// These are the only snapshots in the suite recorded with colour. Every other snapshot runs
    /// through a colourless console, so it records NetPace's wording and holds still when a colour
    /// moves; the snapshots here are the opposite, and exist to make a colour change visible in a
    /// diff.
    /// </remarks>
    public sealed class Themes
    {
        /// <summary>
        /// The arguments that draw the screen a theory row names, where a speed test is the run
        /// with no arguments at all.
        /// </summary>
        private static string[] ArgumentsFor(string screen) => screen == "speed test" ? [] : screen.Split(' ');

        /// <summary>
        /// A colour-recording host for the live speed-test screen: interactive, so the progress
        /// bars render, and colour-capable, so their colours are captured.
        /// </summary>
        private static CommandLineTestHost SpeedTestHost(IServiceCollection services) =>
            new(services, CommandLineTestHost.InteractiveTerminalWidth, interactive: true, recordColour: true);

        /// <summary>
        /// A colour-recording host for a non-interactive run, where no live display renders, at the
        /// width a redirected run sees.
        /// </summary>
        private static CommandLineTestHost StaticScreenHost(IServiceCollection services) =>
            new(services, CommandLineTestHost.RedirectedWidth, recordColour: true);

        /// <summary>
        /// The host the screen a theory row names is drawn on: a terminal for the speed test,
        /// whose default output is the live table and exists only where something can redraw, and
        /// a redirected run for the screens that are the same either way.
        /// </summary>
        private static CommandLineTestHost HostFor(string screen, IServiceCollection services) =>
            screen == "speed test" ? SpeedTestHost(services) : StaticScreenHost(services);

        private static ServiceCollection SpeedTestServices(IThemeProvider themeProvider)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            services.AddSingleton(themeProvider);
            return services;
        }

        private static ServiceCollection ServerListServices(IThemeProvider themeProvider)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton(themeProvider);
            return services;
        }

        /// <summary>
        /// A run whose environment holds the variables given here and nothing else, so what the
        /// user set reaches the run without the suite's own environment being touched.
        /// </summary>
        private static IThemeProvider FromEnvironment(params (string Name, string Value)[] variables) =>
            new EnvironmentThemeProvider(name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault());

        private static IThemeProvider Chosen(Theme theme) => new FixedThemeProvider(theme);

        [Fact]
        public async Task SpeedTest_WithNoThemeChosen_IsDrawnInTodaysColours()
        {
            // SCENARIO: Unset theme keeps today's look

            // Given
            var host = SpeedTestHost(SpeedTestServices(FromEnvironment()));

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task PartlyFinishedTable_WithNoThemeChosen_IsDrawnInTodaysColours()
        {
            // SCENARIO: Unset theme keeps today's look

            // Given a run that ends with one row still measuring, so the table carries every
            // outcome a row can be in at once
            using var cancellation = new CancellationTokenSource();
            var host = SpeedTestHost(PartWayTableServices(FromEnvironment(), cancellation));

            // When
            var result = await host.RunAsync([], cancellation.Token);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task ServerList_WithNoThemeChosen_IsDrawnInTodaysColours()
        {
            // SCENARIO: Unset theme keeps today's look

            // Given
            var host = StaticScreenHost(ServerListServices(FromEnvironment()));

            // When
            var result = await host.RunAsync(["servers", "--fastest"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task HelpScreen_WithNoThemeChosen_IsDrawnInTodaysColours()
        {
            // SCENARIO: Unset theme keeps today's look

            // Given
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment()));

            // When
            var result = await host.RunAsync(["--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task SpeedTest_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            var host = SpeedTestHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "green"))));

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task ServerList_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            var host = StaticScreenHost(ServerListServices(FromEnvironment(("NETPACE_THEME", "green"))));

            // When
            var result = await host.RunAsync(["servers", "--fastest"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task ServerListWithLatency_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            var host = StaticScreenHost(ServerListServices(FromEnvironment(("NETPACE_THEME", "green"))));

            // When
            var result = await host.RunAsync(["servers", "--latency"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task ServersHelpScreen_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            var host = StaticScreenHost(ServerListServices(FromEnvironment(("NETPACE_THEME", "green"))));

            // When
            var result = await host.RunAsync(["servers", "--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task HelpScreen_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "green"))));

            // When
            var result = await host.RunAsync(["--help"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [InlineData("speed test")]
        [InlineData("servers")]
        [InlineData("servers --latency")]
        [InlineData("servers --fastest")]
        [InlineData("--help")]
        [InlineData("servers --help")]
        [Theory]
        public async Task EveryScreen_UnderTheGreenTheme_ChangesColourAndNothingElse(string screen)
        {
            // SCENARIO: Chosen theme applies to every command

            // Given
            string[] args = ArgumentsFor(screen);

            // When the same screen is drawn in each theme
            var asDefault = await Draw(args, Theme.Default);
            var asGreen = await Draw(args, Theme.Green);

            // Then the theme reaches this screen,
            Assert.NotEqual(asDefault, asGreen);

            // and changes only how it is drawn, never what it says.
            Assert.Equal(AnsiSnapshot.WithoutStyling(asDefault), AnsiSnapshot.WithoutStyling(asGreen));

            async Task<string> Draw(string[] arguments, Theme theme)
            {
                var services = SpeedTestServices(Chosen(theme));
                var result = await HostFor(screen, services).RunAsync(arguments);
                Assert.Equal(0, result.ExitCode);
                return AnsiSnapshot.Readable(result.Output);
            }
        }

        [InlineData("speed test")]
        [InlineData("servers")]
        [InlineData("servers --latency")]
        [InlineData("servers --fastest")]
        [InlineData("--help")]
        [InlineData("servers --help")]
        [Theory]
        public async Task EveryScreen_UnderThePlainTheme_CarriesNoColourOrEmphasis(string screen)
        {
            // SCENARIO: Plain theme shows no colour

            // Given
            string[] args = ArgumentsFor(screen);
            var host = HostFor(screen, SpeedTestServices(FromEnvironment(("NETPACE_THEME", "plain"))));

            // When
            var result = await host.RunAsync(args);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
        }

        [Fact]
        public async Task PartlyFinishedTable_UnderThePlainTheme_StillReadsWithoutColour()
        {
            // SCENARIO: Plain theme shows no colour

            // Given a run that ends with one row still measuring, and a partial failure above it
            using var cancellation = new CancellationTokenSource();
            var services = PartWayTableServices(FromEnvironment(("NETPACE_THEME", "plain")), cancellation);
            var host = SpeedTestHost(services);

            // When
            var result = await host.RunAsync([], cancellation.Token);

            // Then nothing is styled, and every row's outcome still reads from its mark and its
            // count rather than from a colour.
            Assert.Equal(0, result.ExitCode);
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task PartlyFinishedTable_UnderTheGreenTheme_IsDrawnInGreen()
        {
            // SCENARIO: Chosen theme applies to every command

            // Given a run that ends with one row still measuring, and a partial failure above it
            using var cancellation = new CancellationTokenSource();
            var host = SpeedTestHost(PartWayTableServices(Chosen(Theme.Green), cancellation));

            // When
            var result = await host.RunAsync([], cancellation.Token);

            // Then the slots only a part-finished table reaches — the spinner beside a row still
            // measuring, its running percentage, and the mark and count of a partial failure — are
            // drawn in the theme. The completed tables in the other green snapshots reach none of
            // these.
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [Fact]
        public async Task UnrecognisedThemeName_RunsAnywayAndNamesTheThemesThatExist()
        {
            // SCENARIO: Unknown theme name

            // Given
            var host = SpeedTestHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"))));

            // When
            var result = await host.RunAsync([]);

            // Then the run completes,
            Assert.Equal(0, result.ExitCode);

            // The notice is a word to the user, never part of the result,
            // and tells them what was not understood and what they could have typed.
            Assert.DoesNotContain("halloween", result.Output, StringComparison.Ordinal);
            await Verify(result.NoticeOutput);
        }

        [InlineData("--csv")]
        [InlineData("--json")]
        [Theory]
        public async Task UnrecognisedThemeName_StaysOutOfMachineReadableOutput(string format)
        {
            // SCENARIO: Unknown theme name

            // Given
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"))));

            // When
            var result = await host.RunAsync([format]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("halloween", result.Output, StringComparison.Ordinal);
        }

        [InlineData("--help")]
        [InlineData("-h")]
        [InlineData("--version")]
        [InlineData("-v")]
        [Theory]
        public async Task UnrecognisedThemeName_OnARunThatNeverParsesOptions_IsStillReported(string screen)
        {
            // SCENARIO: Unknown theme name

            // Given a command that returns before any option is parsed
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"))));

            // When
            var result = await host.RunAsync([screen]);

            // Then the typo is still reported, so it is heard whichever command the user ran.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("halloween", result.NoticeOutput, StringComparison.Ordinal);
        }

        [InlineData("--quiet")]
        [InlineData("-q")]
        [InlineData("--quiet=true")]
        [InlineData("--quiet:true")]
        [InlineData("-q=true")]
        [InlineData("-tq")]
        [Theory]
        public async Task UnrecognisedThemeName_UnderAQuietRun_SaysNothing(string quiet)
        {
            // SCENARIO: Unknown theme name

            // Given
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"))));

            // When
            var result = await host.RunAsync([quiet]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(string.Empty, result.NoticeOutput);
        }

        [InlineData("--quiet", "false")]
        [InlineData("-q", "false")]
        [InlineData("--quiet=false")]
        [Theory]
        public async Task UnrecognisedThemeName_WhenQuietIsTurnedOff_IsStillReported(params string[] args)
        {
            // SCENARIO: Unknown theme name

            // Given
            var host = StaticScreenHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"))));

            // When
            var result = await host.RunAsync(args);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("halloween", result.NoticeOutput, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UnrecognisedThemeName_WhenNoColourIsAsked_NamesThePlainThemeItActuallyDrew()
        {
            // SCENARIO: Unknown theme name

            // Given a typo'd theme name and NO_COLOR, which outranks it
            var host = SpeedTestHost(SpeedTestServices(FromEnvironment(("NETPACE_THEME", "halloween"), ("NO_COLOR", "1"))));

            // When
            var result = await host.RunAsync([]);

            // Then the notice names the theme the run settled on rather than assuming the default,
            // so it does not send the user looking for colour that was never going to appear.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.NoticeOutput);
        }

        [Fact]
        public async Task GreenTheme_WhenTheUserAsksForNoColour_AddsNoColour()
        {
            // SCENARIO: NO_COLOR outranks the theme

            // Given
            var themeProvider = FromEnvironment(("NETPACE_THEME", "green"), ("NO_COLOR", "1"));
            var host = SpeedTestHost(SpeedTestServices(themeProvider));

            // When
            var result = await host.RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
        }

        [InlineData("--csv")]
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [InlineData("--simple")]
        [Theory]
        public async Task MachineReadableOutput_IsTheSameWhateverTheThemeIs(string format)
        {
            // SCENARIO: Machine-readable output ignores the theme

            // Given, When
            var outputs = new List<string>();
            foreach (var theme in Theme.All)
            {
                var host = StaticScreenHost(SpeedTestServices(Chosen(theme)));
                var result = await host.RunAsync([format, "--timestamp"]);
                Assert.Equal(0, result.ExitCode);
                outputs.Add(result.Output);
            }

            // Then - and no theme adds styling of any kind, colour-free emphasis included, which
            // sameness alone would not catch
            Assert.All(outputs, output => Assert.Equal(outputs[0], output));
            Assert.All(outputs, output => Assert.False(AnsiSnapshot.CarriesStyling(output)));
        }

        [InlineData("--csv --json")]
        [InlineData("--definitely-not-an-option")]
        [Theory]
        public async Task AnError_UnderTheGreenTheme_IsDrawnInTheThemesColours(string screen)
        {
            // Given a run that fails, drawn in each theme
            var asDefault = await Draw(ArgumentsFor(screen), Theme.Default);
            var asGreen = await Draw(ArgumentsFor(screen), Theme.Green);

            // Then the theme reaches the error line — a colour hard-coded at the point of writing
            // would leave these two identical,
            Assert.NotEqual(asDefault, asGreen);

            // and changes only how it is drawn, never what it says.
            Assert.Equal(AnsiSnapshot.WithoutStyling(asDefault), AnsiSnapshot.WithoutStyling(asGreen));

            async Task<string> Draw(string[] arguments, Theme theme)
            {
                var result = await StaticScreenHost(SpeedTestServices(Chosen(theme))).RunAsync(arguments);
                Assert.Equal(1, result.ExitCode);
                return AnsiSnapshot.Readable(result.Output);
            }
        }

        [InlineData("default")]
        [InlineData("plain")]
        [InlineData("green")]
        [Theory]
        public async Task AnError_InEveryTheme_ReadsAsAnErrorWithoutTellingColoursApart(string name)
        {
            // Given a theme, and a run asked for two output formats at once
            Theme.TryParse(name, out var theme);
            var host = StaticScreenHost(SpeedTestServices(Chosen(theme)));

            // When
            var result = await host.RunAsync(["--csv", "--json"]);

            // Then the failure is marked by a word, so it survives being read with the colours
            // stripped out — which is what a reader who cannot tell them apart is doing.
            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Error:", AnsiSnapshot.WithoutStyling(result.Output), StringComparison.Ordinal);
        }

        /// <summary>
        /// A service collection whose run ends with the upload still measuring, having measured a
        /// partial failure on the way, so the last frame of the table carries every row outcome
        /// there is: one clean, one partially failed, and one still running.
        /// </summary>
        /// <remarks>
        /// A completed run reaches none of the slots that only a row in flight is drawn with - the
        /// spinner and the running percentage - because by the time the display closes every row
        /// has finished. Ending the run part-way is the only way to draw them.
        /// </remarks>
        private static ServiceCollection PartWayTableServices(IThemeProvider themeProvider, CancellationTokenSource cancellation)
        {
            var partlyFailed = new SpeedTestResult
            {
                BytesProcessed = 1000,
                ElapsedMilliseconds = 1000,
                RequestsSucceeded = 9,
                RequestsFailed = 1,
                Latency = null
            };

            var speedTester = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([new Server { Url = "http://test1.com", Sponsor = "Test Sponsor 1", Location = "Location 1" }]),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) => Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 100, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 0 }),
                GetServerLatencyAsyncFunc = (server, _, _) => Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 100, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 0 }),
                GetDownloadSpeedAsyncFunc = (_, progress, _) =>
                {
                    progress?.Report(new SpeedTestProgress { PercentageComplete = 40, BytesProcessed = 400, ElapsedMilliseconds = 400 });
                    return Task.FromResult(partlyFailed);
                },
                GetUploadSpeedAsyncFunc = (_, _, _) =>
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                    return Task.FromResult(partlyFailed);
                }
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(speedTester);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            services.AddSingleton(themeProvider);
            return services;
        }
    }
}
