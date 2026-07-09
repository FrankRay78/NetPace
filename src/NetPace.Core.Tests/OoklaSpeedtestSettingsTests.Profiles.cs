using NetPace.Core.Clients.Ookla;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Per-profile field-equality assertions for <see cref="OoklaSpeedtestSettings"/>'s
/// inline switch. Each test pins the exact settings a profile is expected to produce.
/// </summary>
public sealed partial class OoklaSpeedtestSettingsTests
{
    [Fact]
    public void Tiny_FieldsMatchDataModel()
    {
        // SCENARIO: Tiny profile stays within IoT budget
        //
        // Natural-transfer budget proxy: ≤ 1 MiB total per run, ≈ 245 KB down + 50 KB up ±10 %.
        // Proxy is recorded here for future readers; not asserted at runtime (there is no
        // Docker integration test that exercises real transfer sizes).

        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Tiny);

        // Then
        s.DownloadTest.DownloadSizes.ShouldBe(new[] { 350 });
        s.DownloadTest.DownloadSizeIterations.ShouldBe(1);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(1);
        s.DownloadTest.DownloadSizeMb.ShouldBe(1);

        s.UploadTest.UploadSizeIncrementKb.ShouldBe(50);
        s.UploadTest.UploadIncrements.ShouldBe(1);
        s.UploadTest.UploadSizeIterations.ShouldBe(1);
        s.UploadTest.UploadParallelTasks.ShouldBe(1);
        s.UploadTest.UploadSizeMb.ShouldBe(1);
    }

    [Fact]
    public void Small_FieldsMatchDataModel()
    {
        // SCENARIO: Small profile suits cellular
        //
        // Natural-transfer budget proxy: ≤ 12 MiB total per run, ≈ 10 MiB down + 2 MiB up ±10 %.

        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Small);

        // Then
        s.DownloadTest.DownloadSizes.ShouldBe(new[] { 1000, 1500 });
        s.DownloadTest.DownloadSizeIterations.ShouldBe(2);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(2);
        s.DownloadTest.DownloadSizeMb.ShouldBe(10);

        s.UploadTest.UploadSizeIncrementKb.ShouldBe(100);
        s.UploadTest.UploadIncrements.ShouldBe(4);
        s.UploadTest.UploadSizeIterations.ShouldBe(2);
        s.UploadTest.UploadParallelTasks.ShouldBe(2);
        s.UploadTest.UploadSizeMb.ShouldBe(2);
    }

    [Fact]
    public void Medium_FieldsMatchDataModel()
    {
        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Medium);

        // Then
        s.DownloadTest.DownloadSizes.ShouldBe(new[] { 1500, 2000, 3000, 3500, 4000 });
        s.DownloadTest.DownloadSizeIterations.ShouldBe(2);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(4);
        s.DownloadTest.DownloadSizeMb.ShouldBe(100);

        s.UploadTest.UploadSizeIncrementKb.ShouldBe(200);
        s.UploadTest.UploadIncrements.ShouldBe(6);
        s.UploadTest.UploadSizeIterations.ShouldBe(5);
        s.UploadTest.UploadParallelTasks.ShouldBe(4);
        s.UploadTest.UploadSizeMb.ShouldBe(25);
    }

    [Fact]
    public void Large_FieldsMatchDataModel()
    {
        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Large);

        // Then
        s.DownloadTest.DownloadSizes.ShouldBe(new[] { 2000, 2500, 3000, 3500, 4000 });
        s.DownloadTest.DownloadSizeIterations.ShouldBe(12);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(16);
        s.DownloadTest.DownloadSizeMb.ShouldBe(1024);

        s.UploadTest.UploadSizeIncrementKb.ShouldBe(500);
        s.UploadTest.UploadIncrements.ShouldBe(8);
        s.UploadTest.UploadSizeIterations.ShouldBe(12);
        s.UploadTest.UploadParallelTasks.ShouldBe(16);
        s.UploadTest.UploadSizeMb.ShouldBe(256);
    }

    [Fact]
    public void Mega_FieldsMatchDataModel()
    {
        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Mega);

        // Then
        s.DownloadTest.DownloadSizes.ShouldBe(new[] { 3000, 4000, 5000, 6000, 7000 });
        s.DownloadTest.DownloadSizeIterations.ShouldBe(40);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(32);
        s.DownloadTest.DownloadSizeMb.ShouldBe(10240);

        s.UploadTest.UploadSizeIncrementKb.ShouldBe(1024);
        s.UploadTest.UploadIncrements.ShouldBe(16);
        s.UploadTest.UploadSizeIterations.ShouldBe(16);
        s.UploadTest.UploadParallelTasks.ShouldBe(32);
        s.UploadTest.UploadSizeMb.ShouldBe(2048);
    }

    [Fact]
    public void DefaultConstructor_CombinedByteCap_Is125MiB()
    {
        // SCENARIO: the default run (no --profile) caps total traffic at the reduced Medium budget.
        //
        // The parameterless constructor resolves to Medium, whose download + upload caps sum to
        // 125 MiB (100 + 25) — a >= 65 % reduction from the pre-profile ~370 MiB default run.
        // This pins the headline figure so a later widening of either Medium cap fails a test.

        // Given / When
        var s = new OoklaSpeedtestSettings();

        // Then
        s.DownloadTest.DownloadSizeMb.ShouldBe(100);
        s.UploadTest.UploadSizeMb.ShouldBe(25);
        (s.DownloadTest.DownloadSizeMb + s.UploadTest.UploadSizeMb).ShouldBe(125);
    }

    [Fact]
    public void Mega_DownloadSizes_ContainsBonusPayloads()
    {
        // SCENARIO: Mega uses bonus payloads

        // Given / When
        var s = new OoklaSpeedtestSettings(Profile.Mega);

        // Then — three separate asserts so a single failure pinpoints which bonus value is missing.
        s.DownloadTest.DownloadSizes.ShouldContain(5000);
        s.DownloadTest.DownloadSizes.ShouldContain(6000);
        s.DownloadTest.DownloadSizes.ShouldContain(7000);
    }
}
