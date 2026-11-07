namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Servers
    {
        /// <summary>
        /// Server details as Speedtest may publish them. A markup cell rejects some outright -
        /// brackets in a location, a sponsor and a URL, and a sponsor reading as an unclosed
        /// styling tag - and changes others without failing: a balanced styling tag, doubled
        /// brackets, and an emoji code. NetPace does not control any of these, so each is listed
        /// character for character.
        /// </summary>
        private static readonly IServer[] unruly =
        [
            new Server { Location = "Bracketed [Town]", Sponsor = "Example [Fibre]", Url = "http://[2001:db8::1]:8080/" },
            new Server { Location = "Emoji Town", Sponsor = ":fire: Networks", Url = "http://emoji.example/" },
            new Server { Location = "Styling Town", Sponsor = "[bold]", Url = "http://styling.example/" },
            new Server { Location = "Tagged Town", Sponsor = "[bold]Fast[/] Fibre", Url = "http://tagged.example/" },
            new Server { Location = "Doubled Town", Sponsor = "Example [[Fibre]]", Url = "http://doubled.example/" },
        ];

        /// <summary>
        /// A service that discovers <see cref="unruly"/> and screens whichever candidates it is
        /// offered to the first of them, at a fixed latency, so every listing is repeatable.
        /// </summary>
        private static SpeedTestMock UnrulyServerService() => new()
        {
            GetServersAsyncFunc = _ => Task.FromResult(unruly),
            GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
                Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 10, JitterMilliseconds = 1 }),
        };

        private static CommandLineTestHost UnrulyServerHost(bool recordColour = false)
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(UnrulyServerService());
            return new CommandLineTestHost(services, recordColour: recordColour);
        }

        /// <summary>
        /// Three plain servers, in the order the listing shows them, so a test naming "the first
        /// row" names the same server however the command orders its rows.
        /// </summary>
        private static readonly IServer[] discoveredServers =
        [
            new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
            new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
            new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
        ];

        /// <summary>
        /// Every form of the <c>servers</c> command, by the command line that runs it: the plain
        /// list, the latency listing and the fastest-server lookup. The exit-code rule is the same
        /// for all three, so each outcome below is checked against every one of them.
        /// </summary>
        public static TheoryData<string> EveryForm() => new("servers", "servers --latency", "servers --fastest");

        private static CommandLineTestHost HostWith(ISpeedTestService speedTestService)
        {
            var services = new ServiceCollection();
            services.AddSingleton(speedTestService);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return new CommandLineTestHost(services);
        }

        [Fact]
        public async Task Should_List_Servers_Whose_Details_Contain_Brackets()
        {
            // SCENARIO: Server details containing brackets are listed as written

            // Given
            var host = UnrulyServerHost();

            // When
            var result = await host.RunAsync(["servers"]);

            // Then every server is listed, with each detail as the server published it.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_List_Servers_Whose_Details_Contain_Brackets_With_Latency()
        {
            // SCENARIO: Server details containing brackets are listed as written

            // Given
            var host = UnrulyServerHost();

            // When
            var result = await host.RunAsync(["servers", "--latency"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_List_The_Fastest_Server_Whose_Details_Contain_Brackets()
        {
            // SCENARIO: Server details containing brackets are listed as written

            // Given
            var host = UnrulyServerHost();

            // When
            var result = await host.RunAsync(["servers", "--fastest"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Draw_A_Server_Detail_That_Reads_As_Styling_Like_Every_Other_Detail()
        {
            // SCENARIO: Server details that look like styling are not styled

            // Given a colour-recording run, so what a sponsor is drawn in is captured rather than
            // stripped.
            var host = UnrulyServerHost(recordColour: true);

            // When
            var result = await host.RunAsync(["servers"]);

            // Then the sponsor reading "[bold]" appears as that literal text, drawn in no colour or
            // emphasis of its own - the same as the sponsors beside it.
            Assert.Equal(0, result.ExitCode);
            await Verify(AnsiSnapshot.Readable(result.Output));
        }

        [InlineData("-f")]
        [InlineData("--fastest")]
        [Theory]
        public async Task Should_Display_Fastest_Speed_Test_Server(string fastest)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", fastest]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers_With_Latency()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers_With_Latency_With_Faulty_Server_Ping()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(new FaultySpeedTester());
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", "-l"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [MemberData(nameof(EveryForm))]
        public async Task Should_Handle_No_Servers_Available(string commandLine)
        {
            // SCENARIO: Servers command with no servers available exits zero

            // Given discovery comes back with nothing at all.
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(Array.Empty<IServer>()),
            });

            // When
            var result = await host.RunAsync(commandLine.Split(' '));

            // Then the user is told no servers were available, and the exit code reports that
            // NetPace itself worked - the same as a speed test that finds none.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Should_Report_That_Nothing_Answered_When_Looking_For_The_Fastest_Server()
        {
            // SCENARIO: Fastest server with none reachable exits zero

            // Given servers are listed, but screening reaches none of them.
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (_, _, _) => throw new NoServersAvailableException(),
            });

            // When
            var result = await host.RunAsync(["servers", "--fastest"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_A_Dash_Against_Every_Server_When_None_Of_Them_Answers()
        {
            // Given servers are listed, but screening reaches none of them.
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (_, _, _) => throw new NoServersAvailableException(),
            });

            // When
            var result = await host.RunAsync(["servers", "--latency"]);

            // Then every row carries "-", as the user guide documents, and the exit code reports
            // that NetPace itself worked.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [MemberData(nameof(EveryForm))]
        public async Task Should_Report_A_Network_Failure_Fetching_The_Server_List(string commandLine)
        {
            // SCENARIO: Servers command during a network outage exits zero

            // Given the server list cannot be fetched at all.
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new HttpRequestException("No such host is known."),
            });

            // When
            var result = await host.RunAsync(commandLine.Split(' '));

            // Then the failure is reported, and the exit code reports that NetPace itself worked.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Should_Report_A_Timed_Out_Server_List_Fetch()
        {
            // SCENARIO: Servers command reports a timed-out list fetch

            // Given fetching the list times out. HttpClient reports its own timeout as a
            // cancellation - of a token that is not the command's - carrying a TimeoutException.
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => throw new TaskCanceledException(
                    "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.",
                    new TimeoutException()),
            });

            // When
            var result = await host.RunAsync(["servers"]);

            // Then the user is told what went wrong, rather than the command stopping silently as
            // though they had pressed Ctrl+C.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Theory]
        [MemberData(nameof(EveryForm))]
        public async Task Should_Stop_Quietly_When_Cancelled_Fetching_The_Server_List(string commandLine)
        {
            // SCENARIO: Cancelling the servers command exits zero without an error

            // Given the user presses Ctrl+C while the server list is being fetched.
            using var cancellation = new CancellationTokenSource();
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ =>
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                },
            });

            // When
            var result = await host.RunAsync(commandLine.Split(' '), cancellation.Token);

            // Then the command stops, nothing is reported, and the exit code is 0.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Should_Stop_Quietly_When_Cancelled_Choosing_The_Fastest_Server()
        {
            // SCENARIO: Cancelling the servers command exits zero without an error

            // Given the user presses Ctrl+C during the lookup that tells them to, after the list
            // has been fetched.
            using var cancellation = new CancellationTokenSource();
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (_, _, _) =>
                {
                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                },
            });

            // When
            var result = await host.RunAsync(["servers", "--fastest"], cancellation.Token);

            // Then no error joins the exit hint already on screen, and the exit code is 0.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Leave_Rows_Blank_That_Cancellation_Cut_Short()
        {
            // SCENARIO: Cancelling the latency listing leaves unfinished rows blank

            // Given the first server answers and the user presses Ctrl+C before the rest do.
            using var cancellation = new CancellationTokenSource();
            var host = HostWith(new SpeedTestMock
            {
                GetServersAsyncFunc = _ => Task.FromResult(discoveredServers),
                GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
                {
                    if (servers[0].Location == discoveredServers[0].Location)
                    {
                        return Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 12, JitterMilliseconds = 1 });
                    }

                    cancellation.Cancel();
                    throw new OperationCanceledException(cancellation.Token);
                },
            });

            // When
            var result = await host.RunAsync(["servers", "--latency"], cancellation.Token);

            // Then the row already measured keeps its figure, the rows cut short are blank rather
            // than "-" - which would have said those servers did not answer - and the exit code
            // reports that NetPace itself worked.
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData("-h")]
        [InlineData("--help")]
        [InlineData("-?")]
        [Theory]
        public async Task Should_Display_Help(string help)
        {
            // SCENARIO: Help shows the lowercase command name

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers", help]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }
    }
}
