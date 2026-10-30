using NetPace.Core.Clients.Ookla;

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

    public static TheoryData<string, string> EveryValueTheHelpLists()
    {
        var data = new TheoryData<string, string>();

        Add("--profile", OptionValues.Profiles);
        Add("--unit", OptionValues.Units);
        Add("--unit-scale", OptionValues.UnitScales);
        Add("--unit-system", OptionValues.UnitSystems);
        Add("--file-mode", OptionValues.FileModes);
        Add("--fail-on", OptionValues.FailOnModes);

        return data;

        void Add<T>(string option, OptionValueSet<T> set) where T : struct, Enum
        {
            foreach (T value in Enum.GetValues<T>())
            {
                data.Add(option, set.Spell(value));
            }
        }
    }

    [Theory]
    [MemberData(nameof(EveryValueTheHelpLists))]
    public async Task EveryValueTheHelpLists_IsAccepted(string option, string value)
    {
        // A value an option can take but the set does not spell arrives here as its enumeration
        // member name, which the parser rejects — so a forgotten spelling fails rather than hiding.

        // Given
        var host = GetOptionValuesTestHost();

        // When
        var result = await host.RunAsync(["--minimal", option, value]);

        // Then
        Assert.Equal(0, result.ExitCode);
    }

    [InlineData("tiny", Profile.Tiny)]
    [InlineData("small", Profile.Small)]
    [InlineData("medium", Profile.Medium)]
    [InlineData("large", Profile.Large)]
    [InlineData("mega", Profile.Mega)]
    [Theory]
    public async Task Profile_EachSpelling_BindsToItsProfile(string value, Profile profile)
    {
        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--profile", value]);

        // Then — assert field-for-field (int[] DownloadSizes uses reference equality).
        Assert.Equal(0, result.ExitCode);
        var expected = new OoklaSpeedtestSettings(profile);
        Assert.Equal(expected.DownloadTest.DownloadSizes, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(expected.DownloadTest.DownloadSizeIterations, accessor.Settings.DownloadTest.DownloadSizeIterations);
        Assert.Equal(expected.DownloadTest.DownloadParallelTasks, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(expected.DownloadTest.DownloadSizeMb, accessor.Settings.DownloadTest.DownloadSizeMb);
        Assert.Equal(expected.UploadTest.UploadSizeIncrementKb, accessor.Settings.UploadTest.UploadSizeIncrementKb);
        Assert.Equal(expected.UploadTest.UploadIncrements, accessor.Settings.UploadTest.UploadIncrements);
        Assert.Equal(expected.UploadTest.UploadSizeIterations, accessor.Settings.UploadTest.UploadSizeIterations);
        Assert.Equal(expected.UploadTest.UploadParallelTasks, accessor.Settings.UploadTest.UploadParallelTasks);
        Assert.Equal(expected.UploadTest.UploadSizeMb, accessor.Settings.UploadTest.UploadSizeMb);
    }

    [InlineData("auto")]
    [InlineData("base")]
    [InlineData("giga")]
    [InlineData("tera")]
    [InlineData("peta")]
    [Theory]
    public async Task UnitScale_EachSpelling_ShowsSpeedsInItsScale(string value)
    {
        // Given
        var host = GetOptionValuesTestHost();

        // When
        var result = await host.RunAsync(["--minimal", "--unit-scale", value]);

        // Then — the snapshot shows the scale each spelling selects.
        Assert.Equal(0, result.ExitCode);
        await Verify(result.Output).UseParameters(value);
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
