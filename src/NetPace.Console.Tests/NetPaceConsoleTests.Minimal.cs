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
            var result = await host.RunAsync(["--minimal"]);

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
            var result = await host.RunAsync(["--minimal"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Show_Minimal_Speeds_In_The_Unit_The_Switches_Ask_For()
        {
            // SCENARIO: Unit switches still shape human-readable output

            // Given the same speed test run twice, once with the default units and once with every
            // unit switch set to something else.
            var defaultUnits = await GetCommandLineTestHost(StubServices()).RunAsync(["--minimal"]);
            var switchedUnits = await GetCommandLineTestHost(StubServices()).RunAsync(
                ["--minimal", "--unit", "bytes", "--unit-scale", "Mega", "--unit-system", "IEC"]);

            // Then neither run reports an error.
            Assert.Equal(0, defaultUnits.ExitCode);
            Assert.Equal(0, switchedUnits.ExitCode);

            // And each run shows its speeds in the unit its command line asked for. The
            // switched run shows 0 MiBps because the stub measures about 1 kB/s down and 2.3
            // kB/s up, which a Mega scale over IEC bytes rounds to zero.
            await Verify(defaultUnits.Output + switchedUnits.Output);
        }

        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }
    }
}
