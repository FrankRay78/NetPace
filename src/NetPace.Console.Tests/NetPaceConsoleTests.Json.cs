namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class Json
    {
        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json(string jsonSwitch)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_Continuously(string jsonSwitch)
        {
            // Given
            var cancellationTokenSource = new CancellationTokenSource();
            var waiter = new SelfCancellingWaiter(10, cancellationTokenSource);

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--loop" ], cancellationTokenSource.Token);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [InlineData("--json", 5)]
        [InlineData("--json-pretty", 5)]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_Multiple_Times(string jsonSwitch, int count)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--count", $"{count}" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, count);
        }

        [InlineData("--json", 10, "00:10:00")]
        [InlineData("--json-pretty", 10, "00:10:00")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_Multiple_Times_With_Delay(string jsonSwitch, int count, string delay)
        {
            // Given
            var waiter = new NoDelayStub();

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--count", $"{count}", "--delay", $"{delay}" ]);

            // Then
            Assert.Equal(count - 1, waiter.CallCount);
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, count, delay);
        }

        [InlineData("--json", "Base")]
        [InlineData("--json", "Kilo")]
        [InlineData("--json", "Mega")]
        [InlineData("--json-pretty", "Base")]
        [InlineData("--json-pretty", "Kilo")]
        [InlineData("--json-pretty", "Mega")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_Multiple_Times_With_Fixed_Scale(string jsonSwitch, string scale)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--count", "3", "--unit-scale", $"{scale}" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, scale);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_No_Download(string jsonSwitch)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--no-download" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_No_Upload(string jsonSwitch)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--no-upload" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_No_Latency(string jsonSwitch)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ jsonSwitch, "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }
    }
}
