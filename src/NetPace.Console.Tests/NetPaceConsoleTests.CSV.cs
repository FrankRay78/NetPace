namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSV
    {
        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Continuously()
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
            var result = await host.RunAsync([ "--csv", "--loop" ], cancellationTokenSource.Token);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData(5)]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times(int count)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--count", $"{count}" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count);
        }

        [InlineData(10, "00:10:00")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_With_Delay(int count, string delay)
        {
            // Given
            var waiter = new NoDelayStub();

            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter>(waiter);
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--count", $"{count}", "--delay", $"{delay}" ]);

            // Then
            Assert.Equal(count - 1, waiter.CallCount);
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count, delay);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_With_Scale_In_Header()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--csv-header-units" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_With_Fixed_Scale()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--count", "3", "--unit-scale", "Mega" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData("Base")]
        [InlineData("Kilo")]
        [InlineData("Mega")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_With_Fixed_Scale_In_Header(string scale)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--csv-header-units", "--count", "3", "--unit-scale", $"{scale}" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(scale);
        }

        [Fact]
        public async Task Should_Return_Validation_Error_For_Speed_Test_With_CSV_Multiple_Times_When_Unit_Scale_Option_Is_Auto()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--csv-header-units", "--count", "3", "--unit-scale", "Auto" ]);

            // Then the validation error is reported on standard error.
            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.Output);
            await Verify(result.Error);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Download()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--no-download" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Upload()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--no-upload" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData(',')]
        [InlineData(';')]
        [InlineData('\t')]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Delimiter(char delimiter)
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--csv-delimiter", delimiter.ToString() ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(delimiter);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Latency()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Header_Units_No_Latency()
        {
            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([ "--csv", "--csv-header-units", "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
