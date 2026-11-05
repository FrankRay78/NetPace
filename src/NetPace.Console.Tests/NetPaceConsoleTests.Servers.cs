namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Servers
    {
        /// <summary>
        /// Server details as Speedtest may publish them: brackets in a location, a sponsor and a
        /// URL, a sponsor that reads as a styling tag, and one that reads as an emoji code. NetPace
        /// does not control any of these, so each is listed character for character.
        /// </summary>
        private static readonly IServer[] unruly =
        [
            new Server { Location = "Bracketed [Town]", Sponsor = "Example [Fibre]", Url = "http://[2001:db8::1]:8080/" },
            new Server { Location = "Emoji Town", Sponsor = ":fire: Networks", Url = "http://emoji.example/" },
            new Server { Location = "Styling Town", Sponsor = "[bold]", Url = "http://styling.example/" },
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
            return new CommandLineTestHost(services, CommandLineTestHost.RedirectedWidth, recordColour: recordColour);
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

        [Fact]
        public async Task Should_Handle_No_Servers_Available()
        {
            // Given
            var mock = new SpeedTestMock
            {
                GetServersAsyncFunc = (cancellationToken) => Task.FromResult(Array.Empty<IServer>()),
                GetFastestServerByLatencyAsyncFunc = (_, _, _) => throw new Exception("No servers available"),
            };

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService>(mock);
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["servers"]);

            // Then
            Assert.Equal(1, result.ExitCode);
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
