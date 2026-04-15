using NetPace.Console.DependencyInjection;

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "servers", fastest ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).DisableRequireUniquePrefix();
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "servers" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers_With_Latency()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "servers", "-l" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Display_Speed_Test_Servers_With_Latency_With_Faulty_Server_Ping()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.RegisterInstance(typeof(ISpeedTestService), new FaultySpeedTester());
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "servers", "-l" ]);

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

            var registrar = new TypeRegistrar();
            registrar.RegisterInstance(typeof(ISpeedTestService), mock);
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync();

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
