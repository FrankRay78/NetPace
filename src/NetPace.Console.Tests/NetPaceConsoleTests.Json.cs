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