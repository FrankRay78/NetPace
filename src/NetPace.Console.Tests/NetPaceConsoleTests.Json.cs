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
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync(jsonSwitch);

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
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar, cancellationTokenSource.Token);

            // When
            var result = await app.RunAsync(jsonSwitch, "--loop");

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
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync(jsonSwitch, "--count", $"{count}");

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, count);
        }

        [InlineData(10, "00:10:00")]
        [Theory]
        public async Task Should_Perform_Speed_Test_With_Json_Multiple_Times_With_Delay(int count, string delay)
        {
            // Given
            var waiter = new NoDelayStub();

            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(IncrementingClockStub));
            registrar.RegisterInstance(typeof(IWaiter), waiter);
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync("--csv", "--count", $"{count}", "--delay", $"{delay}");

            // Then
            Assert.Equal(count - 1, waiter.CallCount);
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(count, delay);
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
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync(jsonSwitch, "--count", "3", "--unit-scale", $"{scale}");

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, scale);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_Json_No_Download()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync("--csv", "--no-download");

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }

        [Fact]
        public async Task Should_Perform_Speed_Test_With_Json_No_Upload()
        {
            // Given
            var registrar = new TypeRegistrar();
            registrar.Register(typeof(ISpeedTestService), typeof(SpeedTestStub));
            registrar.Register(typeof(IClock), typeof(ClockStub));
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync("--csv", "--no-upload");

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output);
        }
    }

    public sealed class Json2
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
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync(jsonSwitch);

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch);
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
            registrar.Register(typeof(IWaiter), typeof(NoDelayStub));
            var app = GetCommandAppTester(registrar);

            // When
            var result = await app.RunAsync(jsonSwitch, "--count", "3", "--unit-scale", $"{scale}");

            // Then
            Assert.Equal(0, result.ExitCode);
            await Verify(result.Output).UseParameters(jsonSwitch, scale);
        }
    }
}