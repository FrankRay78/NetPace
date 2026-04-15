using NetPace.Console.DependencyInjection;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    public sealed class CSV
    {
        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv" ]);

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

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--loop" ], cancellationTokenSource.Token);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData(5)]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times(int count)
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--count", $"{count}" ]);

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

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--count", $"{count}", "--delay", $"{delay}" ]);

            // Then
            Assert.Equal(count - 1, waiter.CallCount);
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count, delay);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_With_Scale_In_Header()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--csv-header-units" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Multiple_Times_With_Fixed_Scale()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--count", "3", "--unit-scale", "Mega" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--csv-header-units", "--count", "3", "--unit-scale", $"{scale}" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(scale);
        }

        [Fact]
        public async Task Should_Return_Validation_Error_For_Speed_Test_With_CSV_Multiple_Times_When_Unit_Scale_Option_Is_Auto()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--csv-header-units", "--count", "3", "--unit-scale", "Auto" ]);

            // Then
            Assert.Equal(-1, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Download()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--no-download" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Upload()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--no-upload" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--csv-delimiter", delimiter.ToString() ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(delimiter);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_No_Latency()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_CSV_Header_Units_No_Latency()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv", "--csv-header-units", "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
        [Fact]
        public async Task Should_Include_IPAddress_And_Hostname_Columns_In_CSV_Output()
        {
            // SCENARIO: CSV header row ends with IPAddress then Hostname columns
            // SCENARIO: CSV data row contains IPAddress and Hostname values matching device identity

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "10.0.0.1", Hostname = "router-a" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_IPAddress_And_Hostname_In_CSV_When_Device_Identity_Unavailable()
        {
            // SCENARIO: CSV IPAddress and Hostname columns contain empty values when device identity is unavailable

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "", Hostname = "" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_In_CSV_When_IP_Retrieval_Fails()
        {
            // SCENARIO: CSV IPAddress column contains ERROR when IP address retrieval raises an exception

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "ERROR", Hostname = "router-a" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_Hostname_In_CSV_When_Hostname_Retrieval_Fails()
        {
            // SCENARIO: CSV Hostname column contains ERROR when hostname retrieval raises an exception
            // SCENARIO: CSV speed test completes and writes output when hostname retrieval raises an exception

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "192.168.1.1", Hostname = "ERROR" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--csv" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }
}
