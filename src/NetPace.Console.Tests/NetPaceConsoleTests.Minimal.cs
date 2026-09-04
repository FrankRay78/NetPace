using Microsoft.Extensions.DependencyInjection;

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
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub());
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--verbosity", "Minimal"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Not_Include_IPAddress_Or_Hostname_In_Minimal_Output_With_Stub_Providing_Error_Values()
        {
            // SCENARIO: Minimal output remains clean even when IClientInfoProvider reports ERROR values

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderErrorStub());
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--verbosity", "Minimal"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
