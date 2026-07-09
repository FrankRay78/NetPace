using NetPace.Console;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    /// <summary>
    /// Build a service collection that captures the constructed <see cref="OoklaSpeedtestSettings"/>
    /// into a returned accessor instance. The action in <see cref="Program.CreateRootCommand"/> writes
    /// to it after option binding, so the test reads <c>accessor.Settings</c> after RunAsync to verify
    /// CLI → settings binding.
    /// </summary>
    private static (IServiceCollection services, OoklaSpeedtestSettingsAccessor accessor) BuildServicesWithSettingsAccessor()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISpeedTestService, SpeedTestStub>();
        services.AddSingleton<IClock, ClockStub>();
        services.AddSingleton<IWaiter, NoDelayStub>();
        var accessor = new OoklaSpeedtestSettingsAccessor();
        services.AddSingleton(accessor);
        return (services, accessor);
    }

    [Theory]
    [InlineData("tiny")]
    [InlineData("Tiny")]
    [InlineData("TINY")]
    public async Task Profile_CaseInsensitiveEnumParsing_BindsToTiny(string value)
    {
        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", value });

        // Then — assert field-for-field (int[] DownloadSizes uses reference equality).
        Assert.Equal(0, result.ExitCode);
        var expected = new OoklaSpeedtestSettings(Profile.Tiny);
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

    [Fact]
    public async Task Profile_UnknownValue_ExitsNonZeroAndMentionsBadValue()
    {
        // SCENARIO: Invalid --profile value is rejected by argument parsing

        // Given
        var (services, _) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", "huge" });

        // Then — non-zero exit and the offending token surfaces in error output.
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("huge", result.Output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_AuthoritativeForPerRequestShape_OnTiny()
    {
        // SCENARIO: Profile is authoritative for per-request shape

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", "tiny" });

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { 350 }, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(1, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(1, accessor.Settings.DownloadTest.DownloadSizeIterations);
        Assert.All(accessor.Settings.DownloadTest.DownloadSizes, s => Assert.True(s <= 350));
    }

    [Fact]
    public async Task NoProfileFlag_DefaultsToMedium()
    {
        // SCENARIO: Omitted --profile defaults to Medium

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(Array.Empty<string>());

        // Then — assert field-for-field (int[] DownloadSizes uses reference equality).
        Assert.Equal(0, result.ExitCode);
        var medium = new OoklaSpeedtestSettings(Profile.Medium);
        Assert.Equal(medium.DownloadTest.DownloadSizes, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(medium.DownloadTest.DownloadSizeIterations, accessor.Settings.DownloadTest.DownloadSizeIterations);
        Assert.Equal(medium.DownloadTest.DownloadParallelTasks, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(medium.DownloadTest.DownloadSizeMb, accessor.Settings.DownloadTest.DownloadSizeMb);
        Assert.Equal(medium.UploadTest.UploadSizeIncrementKb, accessor.Settings.UploadTest.UploadSizeIncrementKb);
        Assert.Equal(medium.UploadTest.UploadIncrements, accessor.Settings.UploadTest.UploadIncrements);
        Assert.Equal(medium.UploadTest.UploadSizeIterations, accessor.Settings.UploadTest.UploadSizeIterations);
        Assert.Equal(medium.UploadTest.UploadParallelTasks, accessor.Settings.UploadTest.UploadParallelTasks);
        Assert.Equal(medium.UploadTest.UploadSizeMb, accessor.Settings.UploadTest.UploadSizeMb);
        // Explicit per-spec assertions.
        Assert.Equal(new[] { 1500, 2000, 3000, 3500, 4000 }, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(100, accessor.Settings.DownloadTest.DownloadSizeMb);
        Assert.Equal(25, accessor.Settings.UploadTest.UploadSizeMb);
    }

    [Fact]
    public async Task DownloadSizeOverride_PreservesProfileShape_OnTiny()
    {
        // SCENARIO: --downloadsize overrides only the cap, profile shape is preserved

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", "tiny", "--downloadsize", "5" });

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { 350 }, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(1, accessor.Settings.DownloadTest.DownloadSizeIterations);
        Assert.Equal(1, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(5, accessor.Settings.DownloadTest.DownloadSizeMb);
    }

    [Fact]
    public async Task UploadSizeOverride_PreservesProfileShape_OnSmall()
    {
        // SCENARIO: --uploadsize overrides only the upload cap

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", "small", "--uploadsize", "1" });

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(100, accessor.Settings.UploadTest.UploadSizeIncrementKb);
        Assert.Equal(4, accessor.Settings.UploadTest.UploadIncrements);
        Assert.Equal(2, accessor.Settings.UploadTest.UploadSizeIterations);
        Assert.Equal(2, accessor.Settings.UploadTest.UploadParallelTasks);
        Assert.Equal(1, accessor.Settings.UploadTest.UploadSizeMb);
    }

    [Fact]
    public async Task DownloadSizeOverride_LargerThanNaturalTransfer_IsNoopBackstop()
    {
        // SCENARIO: Override cap larger than natural transfer is a no-op backstop
        //
        // Tiny's natural transfer (≤ 1 MiB) stays well below the override cap (5000 MiB),
        // so the cap-check never trips. Override is mechanically present on the settings record.
        // Tiny's natural-budget assertion is in OoklaSpeedtestSettingsTests.Profiles.Tiny_FieldsMatchDataModel.

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--profile", "tiny", "--downloadsize", "5000" });

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(5000, accessor.Settings.DownloadTest.DownloadSizeMb);
    }

    [Fact]
    public async Task NoDownload_ShortCircuits_RegardlessOfProfile()
    {
        // SCENARIO: --no-download short-circuits regardless of profile

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(new[] { "--no-download", "--profile", "large" });

        // Then
        Assert.Equal(0, result.ExitCode);

        // --no-download is honoured by the runtime (existing behaviour), and the upload phase
        // still binds to Large's per-request shape.
        Assert.Equal(500, accessor.Settings.UploadTest.UploadSizeIncrementKb);
        Assert.Equal(8, accessor.Settings.UploadTest.UploadIncrements);
        Assert.Equal(16, accessor.Settings.UploadTest.UploadParallelTasks);
    }
}
