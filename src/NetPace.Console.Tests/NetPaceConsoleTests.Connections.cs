using NetPace.Core;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

public sealed partial class NetPaceConsoleTests
{
    [Fact]
    public async Task Connections_OverridesProfileCount_LeavingProfileCapsIntact()
    {
        // SCENARIO: Connection count overrides the profile

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--profile", "large", "--connections", "2"]);

        // Then neither direction may run more than two transfers at once, while Large's
        // total-byte caps and per-request shape are untouched.
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(2, accessor.Settings.UploadTest.UploadParallelTasks);

        var large = new OoklaSpeedtestSettings(Profile.Large);
        Assert.Equal(large.DownloadTest.DownloadSizeMb, accessor.Settings.DownloadTest.DownloadSizeMb);
        Assert.Equal(large.UploadTest.UploadSizeMb, accessor.Settings.UploadTest.UploadSizeMb);
        Assert.Equal(large.DownloadTest.DownloadSizes, accessor.Settings.DownloadTest.DownloadSizes);
        Assert.Equal(large.DownloadTest.DownloadSizeIterations, accessor.Settings.DownloadTest.DownloadSizeIterations);
        Assert.Equal(large.UploadTest.UploadSizeIncrementKb, accessor.Settings.UploadTest.UploadSizeIncrementKb);
        Assert.Equal(large.UploadTest.UploadIncrements, accessor.Settings.UploadTest.UploadIncrements);
        Assert.Equal(large.UploadTest.UploadSizeIterations, accessor.Settings.UploadTest.UploadSizeIterations);
    }

    [Fact]
    public async Task Connections_One_RunsOneTransferAtATimeInEachDirection()
    {
        // SCENARIO: A single connection

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--connections", "1"]);

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(1, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(1, accessor.Settings.UploadTest.UploadParallelTasks);
    }

    [Fact]
    public async Task NoConnectionsFlag_ProfileConnectionCountApplies()
    {
        // SCENARIO: Without the option the profile decides

        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--profile", "small"]);

        // Then
        Assert.Equal(0, result.ExitCode);
        var small = new OoklaSpeedtestSettings(Profile.Small);
        Assert.Equal(small.DownloadTest.DownloadParallelTasks, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(small.UploadTest.UploadParallelTasks, accessor.Settings.UploadTest.UploadParallelTasks);
        // Explicit per-spec assertions: Small runs two transfers at once in each direction.
        Assert.Equal(2, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(2, accessor.Settings.UploadTest.UploadParallelTasks);
    }

    [Theory]
    [InlineData("0", "zero")]
    [InlineData("33", "above-range")]
    [InlineData("2.5", "fraction")]
    [InlineData("lots", "words")]
    public async Task Connections_OutOfRangeOrNotAWholeNumber_IsRejectedBeforeAnyTestRuns(string value, string valueName)
    {
        // SCENARIO: An out-of-range connection count is rejected

        // Given
        var (services, _) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--connections", value]);

        // Then the run is refused up front and nothing is measured — the snapshot is the whole
        // output, so a result line appearing would fail it.
        Assert.NotEqual(0, result.ExitCode);

        // The snapshot is named for the rejected value in words: "2.5" and "0" are not equally
        // safe filename characters across platforms.
        await Verify(result.Output).UseTextForParameters(valueName);
    }

    [Theory]
    [InlineData("--minimal")]
    [InlineData("--csv")]
    [InlineData("--json")]
    [InlineData("--json-pretty")]
    public async Task Connections_AppliesUnderEveryOutputFormat(string formatSwitch)
    {
        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When
        var result = await host.RunAsync(["--connections", "3", formatSwitch]);

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(3, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(3, accessor.Settings.UploadTest.UploadParallelTasks);
    }

    [Fact]
    public async Task Connections_AppliesToEveryRunUnderCount()
    {
        // Given
        var (services, accessor) = BuildServicesWithSettingsAccessor();
        var host = GetCommandLineTestHost(services);

        // When — the settings are built once and handed to the provider for the whole invocation,
        // so every run of a repeated test uses the same connection count.
        var result = await host.RunAsync(["--connections", "3", "--count", "3", "--minimal"]);

        // Then
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(3, accessor.Settings.DownloadTest.DownloadParallelTasks);
        Assert.Equal(3, accessor.Settings.UploadTest.UploadParallelTasks);
    }
}
