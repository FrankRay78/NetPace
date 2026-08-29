namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Servers
    {
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
            var result = await host.RunAsync([ "servers", fastest ]);

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
            var result = await host.RunAsync([ "servers" ]);

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
            var result = await host.RunAsync([ "servers", "-l" ]);

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
            var result = await host.RunAsync([ "servers", "-l" ]);

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
            var result = await host.RunAsync([ "servers" ]);

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
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "servers", help ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }
    }
}
