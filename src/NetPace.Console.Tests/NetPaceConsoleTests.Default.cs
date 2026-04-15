using NetPace.Console.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Default
    {
        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Default_Output()
        {
            // SCENARIO: Default rich terminal output does not include IPAddress or Hostname

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub());
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("IPAddress", result.Output);
            Assert.DoesNotContain("Hostname", result.Output);
        }

        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Default_Output_With_Stub_Providing_Error_Values()
        {
            // SCENARIO: Default rich terminal output does not include IPAddress or Hostname

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderErrorStub());
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.DoesNotContain("IPAddress", result.Output);
            Assert.DoesNotContain("Hostname", result.Output);
        }
    }
}
