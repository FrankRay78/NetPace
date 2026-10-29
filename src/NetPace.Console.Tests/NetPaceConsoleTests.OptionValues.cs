using NetPace.Console;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    private static CommandLineTestHost GetOptionValuesTestHost()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        return GetCommandLineTestHost(services);
    }

    [InlineData("--profile", "huge")]
    [InlineData("--unit", "BitsPerSecond")]
    [InlineData("--unit-scale", "Enormous")]
    [InlineData("--unit-system", "Imperial")]
    [InlineData("--file-mode", "Truncate")]
    [InlineData("--fail-on", "Everything")]
    [Theory]
    public async Task UnknownOptionValue_IsRejectedAndTheValidValuesAreListed(string option, string value)
    {
        // SCENARIO: Rejected value lists the valid values as the help shows them

        // Given
        var host = GetOptionValuesTestHost();

        // When
        var result = await host.RunAsync([option, value]);

        // Then
        Assert.NotEqual(0, result.ExitCode);
        await Verify(result.Output).UseParameters(option, value);
    }

    [InlineData("BitsPerSecond")]
    [InlineData("BytesPerSecond")]
    [InlineData("bitspersecond")]
    [Theory]
    public async Task Unit_RejectsTheFormerPerSecondSpellings(string value)
    {
        // Given
        var host = GetOptionValuesTestHost();

        // When
        var result = await host.RunAsync(["--unit", value]);

        // Then
        Assert.NotEqual(0, result.ExitCode);
    }

    [InlineData("bits", "Bits", "BITS")]
    [InlineData("bytes", "Bytes", "BYTES")]
    [Theory]
    public async Task Unit_IsAcceptedInAnyCase(string lower, string mixed, string upper)
    {
        // Given — a host per run; one host captures output for a single run only.
        // When
        var lowerResult = await GetOptionValuesTestHost().RunAsync(["--minimal", "--unit", lower]);
        var mixedResult = await GetOptionValuesTestHost().RunAsync(["--minimal", "--unit", mixed]);
        var upperResult = await GetOptionValuesTestHost().RunAsync(["--minimal", "--unit", upper]);

        // Then — all three spellings are accepted and report the same speeds.
        Assert.Equal(0, lowerResult.ExitCode);
        Assert.Equal(lowerResult.Output, mixedResult.Output);
        Assert.Equal(lowerResult.Output, upperResult.Output);
    }
}
