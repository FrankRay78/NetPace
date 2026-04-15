using NetPace.Console.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Minimal
    {
        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Minimal_Output()
        {
            // SCENARIO: Minimal output does not include IPAddress or Hostname

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub());
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--verbosity", "Minimal" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

    }
}
