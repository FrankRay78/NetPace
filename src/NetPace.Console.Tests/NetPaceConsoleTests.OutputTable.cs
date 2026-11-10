using Microsoft.Extensions.DependencyInjection;
using NetPace.Console.Tests.Theming;
using NetPace.Console.Theming;
using NetPace.Core;
using NetPace.Core.Clients.Testing;
using Spectre.Console;
using Spectre.Console.Testing;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Default output at a terminal: the live table that replaces the progress bars and the result
    /// line, and the <c>--simple</c> lines it writes instead wherever it cannot redraw.
    /// </summary>
    public sealed class OutputTable
    {
        private static ServiceCollection Services(ISpeedTestService service)
        {
            var services = new ServiceCollection();
            services.AddSingleton(service);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        private static CommandLineTestHost TerminalHost(IServiceCollection services, int width = CommandLineTestHost.InteractiveTerminalWidth) =>
            new(services, width, interactive: true);

        private static CommandLineTestHost RedirectedHost(IServiceCollection services) =>
            new(services, CommandLineTestHost.RedirectedWidth);

        /// <summary>A transfer result carrying latency measured under its own load.</summary>
        private static SpeedTestResult Transfer(int succeeded, int failed, long? latencyMilliseconds, long jitterMilliseconds = 4) =>
            new()
            {
                BytesProcessed = succeeded == 0 ? 0 : 1_000_000,
                ElapsedMilliseconds = 1000,
                RequestsSucceeded = succeeded,
                RequestsFailed = failed,
                Latency = latencyMilliseconds is long latency
                    ? new LatencyTestResult
                    {
                        Server = new Server { Url = ScriptedSpeedTester.Url, Sponsor = "Deutsche Telekom" },
                        LatencyMilliseconds = latency,
                        MinimumLatencyMilliseconds = null,
                        MaximumLatencyMilliseconds = null,
                        JitterMilliseconds = jitterMilliseconds
                    }
                    : null
            };

        [Fact]
        public async Task A_Named_Server_Is_Labelled_With_Square_Brackets_And_Chooses_Nothing()
        {
            // SCENARIO: A named server is labelled with square brackets

            // Given a terminal and a server the user named, so there is nothing to choose.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync(["--server-url", "http://test1.com"]);

            // Then the header carries the placeholder sponsor in square brackets above the URL,
            // and no "Choosing server" spinner appears for a choice that was never made - both of
            // which the snapshot carries.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task The_Server_Is_On_Screen_Before_Any_Latency_Reply()
        {
            // SCENARIO: The server is shown before any latency reply

            // Given a terminal, and a service that records what the screen already said at the
            // moment it is asked to measure the chosen server's latency.
            var console = new TestConsole().Width(CommandLineTestHost.InteractiveTerminalWidth);
            console.Interactive();

            var screenWhenAsked = string.Empty;
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([new Server { Location = "Frankfurt", Sponsor = "Hey! Broadband", Url = ScriptedSpeedTester.Url }]),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) => Task.FromResult(new LatencyTestResult
                {
                    Server = servers[0],
                    LatencyMilliseconds = 3,
                    MinimumLatencyMilliseconds = null,
                    MaximumLatencyMilliseconds = null,
                    JitterMilliseconds = 1
                }),
                GetServerLatencyAsyncFunc = (server, _, _) =>
                {
                    screenWhenAsked = console.Output;
                    return Task.FromResult(new LatencyTestResult
                    {
                        Server = server,
                        LatencyMilliseconds = 3,
                        MinimumLatencyMilliseconds = null,
                        MaximumLatencyMilliseconds = null,
                        JitterMilliseconds = 1
                    });
                },
                GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(Transfer(150, 0, 60)),
                GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(Transfer(32, 0, 38)),
            };

            // And a display that redraws as it goes, as a real terminal's does, so the screen holds
            // whatever had been drawn by then rather than only the final frame.
            var services = Services(service);
            services.AddSingleton<IAnsiConsole>(console);
            services.AddSingleton<ILiveDisplayRefresh, LiveDisplayRefresh>();

            // When
            var result = await TerminalHost(services).RunAsync([]);

            // Then the sponsor, the URL and the running Pinging row were already on screen before
            // the first probe went out, so a slow line names the server it is testing and shows
            // the measurement under way rather than a blank screen.
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Hey! Broadband", screenWhenAsked);
            Assert.Contains(ScriptedSpeedTester.Url, screenWhenAsked);
            Assert.Contains("Pinging", screenWhenAsked);
        }

        [Fact]
        public async Task Each_Figure_Appears_Beside_The_Test_It_Belongs_To()
        {
            // SCENARIO: Figures appear beside the test they belong to

            // Given a terminal and a run of all three tests.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync([]);

            // Then idle latency and jitter sit on the Pinging row, each transfer's speed, latency
            // and jitter on its own row, and no result line follows the table.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Transfer_That_Measured_No_Latency_Leaves_Its_Cells_Blank()
        {
            // Given a terminal and transfers whose results say latency under load was not measured.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(150, 0, null),
                UploadFactory = _ => Transfer(32, 0, null),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Partial_Failure_Keeps_Its_Figures_Beside_The_Count()
        {
            // Given a terminal and a download in which some requests failed.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(149, 1, 60),
                UploadFactory = _ => Transfer(32, 0, 38),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Total_Failure_Shows_A_Zero_Speed_And_No_Latency()
        {
            // Given a terminal and an upload in which every request failed.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(150, 0, 60),
                UploadFactory = _ => Transfer(0, 30, null),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Every_Column_Is_Drawn_On_A_Terminal_Too_Narrow_For_The_Table()
        {
            // SCENARIO: Jitter gives way on a narrow terminal

            // Given a terminal too narrow for the full table.
            var host = TerminalHost(Services(new SpeedTestStub()), CommandLineTestHost.NarrowTerminalWidth);

            // When
            var result = await host.RunAsync([]);

            // Then every column is still drawn: both latency figures are on screen, so the table
            // wrapped itself to fit rather than dropping a figure the user asked for. A dropped
            // column would read as a measurement that was never taken.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task No_Latency_Leaves_The_Table_Without_A_Pinging_Row()
        {
            // SCENARIO: No latency means no latency anywhere

            // Given a terminal and --no-latency.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync(["--no-latency"]);

            // Then nothing in the table names latency at all.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task No_Latency_Sends_No_Under_Load_Probes()
        {
            // Given a --no-latency run
            var (services, accessor) = BuildServicesWithSettingsAccessor();

            // When
            var result = await new CommandLineTestHost(services).RunAsync(["--no-latency"]);

            // Then no probe goes out while a transfer runs either: asking for no latency asks for
            // none anywhere, in whichever format the run reports.
            Assert.Equal(0, result.ExitCode);
            Assert.False(accessor.Settings.LatencyTest.LoadedLatencyEnabled);
        }

        [Theory]
        [InlineData("--no-download")]
        [InlineData("--no-upload")]
        public async Task A_Test_That_Was_Switched_Off_Has_No_Row(string switchedOff)
        {
            // Given a terminal and one transfer switched off.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync([switchedOff]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(switchedOff);
        }

        [Fact]
        public async Task Both_Transfers_Switched_Off_Leaves_Only_The_Pinging_Row()
        {
            // Given a terminal and both transfers switched off.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync(["--no-download", "--no-upload"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Repeated_Run_Draws_The_Whole_Block_Each_Time()
        {
            // Given a terminal and two runs asked for.
            var host = TerminalHost(Services(new SpeedTestStub()));

            // When
            var result = await host.RunAsync(["--count", "2"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Output_That_Cannot_Animate_Is_Exactly_What_Simple_Writes()
        {
            // SCENARIO: Output that cannot animate falls back to simple

            // Given default output going to a pipe or a redirect.
            var defaultRun = await RedirectedHost(Services(new SpeedTestStub())).RunAsync([]);

            // When the same run is asked for --simple instead.
            var simpleRun = await RedirectedHost(Services(new SpeedTestStub())).RunAsync(["--simple"]);

            // Then the two are the same output, down to the line: a destination that cannot redraw
            // gets the stable format rather than a table drawn with no cursor to move.
            Assert.Equal(0, defaultRun.ExitCode);
            Assert.Equal(simpleRun.Output, defaultRun.Output);
        }

        [Fact]
        public async Task A_File_Gets_Simple_Output_While_The_Terminal_Gets_The_Table()
        {
            // SCENARIO: A file gets simple output while the terminal gets the table

            // Given a terminal and --file.
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                // When
                var result = await TerminalHost(Services(new SpeedTestStub())).RunAsync(["--file", path]);

                // Then the terminal shows the table.
                Assert.Equal(0, result.ExitCode);
                Assert.Contains("Pinging", result.Output);
                Assert.Contains("Downloading", result.Output);

                // And the file holds what --simple writes, with no control code in it.
                var simpleRun = await RedirectedHost(Services(new SpeedTestStub())).RunAsync(["--simple"]);
                var written = await System.IO.File.ReadAllTextAsync(path);

                Assert.Equal(Normalise(simpleRun.Output), Normalise(written));
                Assert.DoesNotContain('\u001b', written);
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Fact]
        public async Task A_Failed_Idle_Measurement_Still_Names_The_Server()
        {
            // SCENARIO: A failed idle measurement still names the server

            // Given a terminal and an idle measurement that fails.
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = ScriptedSpeedTester.Url }]),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) => Task.FromResult(new LatencyTestResult
                {
                    Server = servers[0],
                    LatencyMilliseconds = 3,
                    MinimumLatencyMilliseconds = null,
                    MaximumLatencyMilliseconds = null,
                    JitterMilliseconds = 1
                }),
                GetServerLatencyAsyncFunc = (_, _, _) => throw new HttpRequestException("The server did not answer"),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then the server and URL are still on screen, the Pinging row shows the failure, and
            // the run reports the error.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Probe_That_Times_Out_Leaves_No_Row_Looking_Like_It_Is_Still_Running()
        {
            // Given a terminal and an idle measurement whose probe timed out, which surfaces as a
            // cancellation exception although nobody cancelled the run.
            var service = new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = ScriptedSpeedTester.Url }]),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) => Task.FromResult(new LatencyTestResult
                {
                    Server = servers[0],
                    LatencyMilliseconds = 3,
                    MinimumLatencyMilliseconds = null,
                    MaximumLatencyMilliseconds = null,
                    JitterMilliseconds = 1
                }),
                GetServerLatencyAsyncFunc = (_, _, _) => throw new TaskCanceledException("The probe timed out", new TimeoutException()),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then every row is marked as failed rather than left spinning, as for any other
            // failure: a timeout is the server not answering, not the user stopping the run.
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Transfer_That_Throws_Leaves_No_Row_Looking_Like_It_Is_Still_Running()
        {
            // Given a terminal, a latency measurement that succeeds and a download that throws.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => throw new HttpRequestException("The connection was reset"),
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([]);

            // Then no row is left carrying a spinner: the last frame on screen is drawn after the
            // run has ended, so a row still marked as measuring would say a measurement is in
            // flight after the process has exited. The snapshot carries what each row says.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Cancelled_Run_Keeps_The_Figures_Measured_So_Far()
        {
            // Given a terminal and a run cancelled while the upload is in flight.
            using var cancellation = new CancellationTokenSource();

            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(150, 0, 60),
                UploadFactory = _ =>
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                    return Transfer(32, 0, 38);
                },
            };

            // When
            var result = await TerminalHost(Services(service)).RunAsync([], cancellation.Token);

            // Then the download's figures are still on screen.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task A_Cancelled_Run_Leaves_The_Cursor_Showing()
        {
            // Given a colour-recording terminal, so the control codes the run writes are captured,
            // and a run cancelled while the upload is in flight.
            using var cancellation = new CancellationTokenSource();

            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(150, 0, 60),
                UploadFactory = _ =>
                {
                    cancellation.Cancel();
                    cancellation.Token.ThrowIfCancellationRequested();
                    return Transfer(32, 0, 38);
                },
            };

            var host = new CommandLineTestHost(Services(service), CommandLineTestHost.InteractiveTerminalWidth, interactive: true, recordColour: true);

            // When
            var result = await host.RunAsync([], cancellation.Token);

            // Then the live display put the cursor back before the run ended, so the terminal is
            // left usable rather than with an invisible cursor.
            Assert.Equal(0, result.ExitCode);
            Assert.EndsWith(ShowCursor, result.Output, StringComparison.Ordinal);
        }

        /// <summary>
        /// The sequence a terminal is sent to show its cursor again, which a live display hides
        /// while it is drawing.
        /// </summary>
        private const string ShowCursor = "\u001b[?25h";

        [Fact]
        public async Task The_Plain_Theme_Leaves_The_Table_Unstyled_And_Still_Readable()
        {
            // SCENARIO: The plain theme reads without colour

            // Given the plain theme, a colour-capable terminal, and a run with a partial failure.
            var service = new ScriptedSpeedTester
            {
                DownloadFactory = _ => Transfer(149, 1, 60),
                UploadFactory = _ => Transfer(32, 0, 38),
            };

            var services = Services(service);
            services.AddSingleton<IThemeProvider>(new FixedThemeProvider(Theme.Plain));
            var host = new CommandLineTestHost(services, CommandLineTestHost.InteractiveTerminalWidth, interactive: true, recordColour: true);

            // When
            var result = await host.RunAsync([]);

            // Then nothing is styled, and the failure can still be read from the symbol and the
            // count rather than from a colour.
            Assert.Equal(0, result.ExitCode);
            Assert.False(AnsiSnapshot.CarriesStyling(result.Output));
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        /// <summary>
        /// Line endings as a comparison needs them: the terminal and the file writer each use their
        /// own, and the comparison is about what the lines say.
        /// </summary>
        private static string Normalise(string output) => output.ReplaceLineEndings("\n");
    }
}
