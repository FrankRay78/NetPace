using System.Text.Json;

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
            var result = await host.RunAsync([jsonSwitch]);

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
            var result = await host.RunAsync([jsonSwitch, "--loop"], cancellationTokenSource.Token);

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
            var result = await host.RunAsync([jsonSwitch, "--count", $"{count}"]);

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
            var result = await host.RunAsync([jsonSwitch, "--count", $"{count}", "--delay", $"{delay}"]);

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
        public async Task Should_Perform_Speed_Test_With_Json_Multiple_Times_Under_Every_Unit_Scale(string jsonSwitch, string scale)
        {
            // Given a tester whose speed changes between runs, so a scale that still reached
            // JSON would have shown different units from one result to the next.
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, VariableSpeedTester>();
            services.AddSingleton<IClock, IncrementingClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync([jsonSwitch, "--count", "3", "--unit-scale", $"{scale}"]);

            // Then the run is accepted and every result carries bare numbers in the same fixed units.
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
            var result = await host.RunAsync([jsonSwitch, "--no-download"]);

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
            var result = await host.RunAsync([jsonSwitch, "--no-upload"]);

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
            var result = await host.RunAsync([jsonSwitch, "--no-latency"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [Fact]
        public async Task Should_Include_IPv6_In_Json_Output_When_No_IPv4_Available()
        {
            // SCENARIO: JSON IPAddress field contains first IPv6 address when no IPv4 is available

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "2001:db8::1" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_IPAddress_In_Json_Output_When_No_Network_Interfaces()
        {
            // SCENARIO: JSON IPAddress field is empty string when no network interfaces are available

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Empty_Hostname_In_Json_Output_When_Hostname_Resolves_Empty()
        {
            // SCENARIO: JSON Hostname field is empty string when the OS hostname resolves to empty

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { Hostname = "" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_In_Json_Output_When_IP_Retrieval_Fails()
        {
            // SCENARIO: JSON IPAddress field contains ERROR when IP address retrieval raises an exception
            // SCENARIO: JSON speed test completes and writes output when IP address retrieval raises an exception

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "ERROR", Hostname = "test-host" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_Hostname_In_Json_Output_When_Hostname_Retrieval_Fails()
        {
            // SCENARIO: JSON Hostname field contains ERROR when hostname retrieval raises an exception

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "192.168.1.1", Hostname = "ERROR" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Include_Error_IPAddress_And_Hostname_In_Json_Output_When_Both_Retrievals_Fail()
        {
            // SCENARIO: JSON speed test completes and writes output when both device identity lookups raise exceptions

            // Given
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IClientInfoProvider>(new ClientInfoProviderStub { IPAddress = "ERROR", Hostname = "ERROR" });
            services.AddSingleton<IWaiter, NoDelayStub>();
            var host = GetCommandLineTestHost(services);

            // When
            var result = await host.RunAsync(["--json"]);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Report_Speed_And_Latency_As_Json_Numbers(string jsonSwitch)
        {
            // SCENARIO: JSON reports speed and latency only as numbers
            // SCENARIO: JSON reports speed and latency as numbers in fixed units

            // Given a stub that measures 100 ms of latency, 1000 bytes downloaded in 1000 ms
            // (8,000 bits per second) and 7000 bytes uploaded in 3000 ms (18,666.67, rounded).
            var host = GetCommandLineTestHost(StubServices());

            // When
            var result = await host.RunAsync([jsonSwitch]);

            // Then each measurement appears once, as a number in a fixed unit.
            Assert.Equal(0, result.ExitCode);
            var json = ParseJsonResult(result.Output);
            Assert.Equal(JsonValueKind.Number, json.GetProperty("LatencyMilliseconds").ValueKind);
            Assert.Equal(JsonValueKind.Number, json.GetProperty("DownloadSpeedBitsPerSecond").ValueKind);
            Assert.Equal(JsonValueKind.Number, json.GetProperty("UploadSpeedBitsPerSecond").ValueKind);
            Assert.Equal(100, json.GetProperty("LatencyMilliseconds").GetInt64());
            Assert.Equal(8000, json.GetProperty("DownloadSpeedBitsPerSecond").GetInt64());
            Assert.Equal(18667, json.GetProperty("UploadSpeedBitsPerSecond").GetInt64());

            // And the fields that held a formatted speed or latency are no longer written.
            Assert.False(json.TryGetProperty("Latency", out _));
            Assert.False(json.TryGetProperty("DownloadSpeed", out _));
            Assert.False(json.TryGetProperty("UploadSpeed", out _));

            // And the whole result, so the full user-visible contract is reviewable.
            await Verify(result.Output).UseParameters(jsonSwitch);
        }

        [InlineData("--json")]
        [InlineData("--json-pretty")]
        [Theory]
        public async Task Should_Report_The_Same_Json_Whatever_The_Unit_Switches_Say(string jsonSwitch)
        {
            // SCENARIO: Unit switches do not change JSON output

            // Given the same speed test run twice, once with the default units and once with every
            // unit switch set to something else.
            var defaultUnits = await GetCommandLineTestHost(StubServices()).RunAsync([jsonSwitch]);
            var switchedUnits = await GetCommandLineTestHost(StubServices()).RunAsync(
                [jsonSwitch, "--unit", "BytesPerSecond", "--unit-scale", "Mega", "--unit-system", "IEC"]);

            // Then neither run reports an error, and the two outputs are the same.
            Assert.Equal(0, defaultUnits.ExitCode);
            Assert.Equal(0, switchedUnits.ExitCode);
            Assert.Equal(defaultUnits.Output, switchedUnits.Output);
        }

        private static ServiceCollection StubServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton<ISpeedTestService, SpeedTestStub>();
            services.AddSingleton<IClock, ClockStub>();
            services.AddSingleton<IWaiter, NoDelayStub>();
            return services;
        }

        /// <summary>
        /// Parses the one JSON object a single speed test writes. The returned element is owned by a
        /// disposed document, so it is cloned to stay readable.
        /// </summary>
        private static JsonElement ParseJsonResult(string output)
        {
            using var document = JsonDocument.Parse(output);
            return document.RootElement.Clone();
        }
    }
}
