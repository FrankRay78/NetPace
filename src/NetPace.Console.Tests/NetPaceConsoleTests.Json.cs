using NetPace.Console.DependencyInjection;

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch ]);

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

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--loop" ], cancellationTokenSource.Token);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--count", $"{count}" ]);

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

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--count", $"{count}", "--delay", $"{delay}" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(VariableSpeedTester));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--count", "3", "--unit-scale", $"{scale}" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--no-download" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--no-upload" ]);

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
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ jsonSwitch, "--no-latency" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }
        [Fact]
        public async Task Should_Include_IPAddress_And_Hostname_In_Json_Output()
        {
            // SCENARIO: JSON output contains IPAddress field populated with device IPv4 address
            // SCENARIO: JSON output contains Hostname field populated with device hostname
            // SCENARIO: IPAddress field appears after UploadSpeed in JSON output
            // SCENARIO: Hostname field appears after IPAddress in JSON output

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_IPv6_In_Json_Output_When_No_IPv4_Available()
        {
            // SCENARIO: JSON IPAddress field contains first IPv6 address when no IPv4 is available

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "2001:db8::1" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"IPAddress\":\"2001:db8::1\"", result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_IPAddress_In_Json_Output_When_No_Network_Interfaces()
        {
            // SCENARIO: JSON IPAddress field is empty string when no network interfaces are available

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"IPAddress\":\"\"", result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_In_Json_Output_When_IP_Retrieval_Fails()
        {
            // SCENARIO: JSON IPAddress field contains ERROR when IP address retrieval raises an exception

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "ERROR", Hostname = "test-host" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"IPAddress\":\"ERROR\"", result.Output);
            Assert.Contains("\"Hostname\"", result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_Hostname_In_Json_Output_When_Hostname_Retrieval_Fails()
        {
            // SCENARIO: JSON Hostname field contains ERROR when hostname retrieval raises an exception

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { IPAddress = "192.168.1.1", Hostname = "ERROR" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"Hostname\":\"ERROR\"", result.Output);
            Assert.Contains("\"IPAddress\"", result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_Hostname_In_Json_Output_When_Hostname_Resolves_Empty()
        {
            // SCENARIO: JSON Hostname field is empty string when the OS hostname resolves to empty

            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.RegisterInstance(typeof(IClientInfoProvider), new ClientInfoProviderStub { Hostname = "" });
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync([ "--json" ]);

            // Then
            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"Hostname\":\"\"", result.Output);
        }
    }
}