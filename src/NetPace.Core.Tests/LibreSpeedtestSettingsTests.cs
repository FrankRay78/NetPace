using NetPace.Core.Clients.LibreSpeed;
using NetPace.Core.Clients.Ookla;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// What a profile means to a LibreSpeed run: the same data ceilings and the same connection
/// counts an Ookla run gets, with LibreSpeed's own request sizes under them.
/// </summary>
public sealed class LibreSpeedtestSettingsTests
{
    public static TheoryData<Profile> EveryProfile() =>
        new(Enum.GetValues<Profile>());

    [Theory]
    [MemberData(nameof(EveryProfile))]
    public void Profile_CeilingsAndConnections_MatchOoklas(Profile profile)
    {
        // The profile's data ceilings and connection counts come from one shared table, so the two
        // providers cannot drift apart; only the request sizes under them are each provider's own.

        // Given / When
        var libreSpeed = new LibreSpeedtestSettings(profile);
        var ookla = new OoklaSpeedtestSettings(profile);

        // Then
        libreSpeed.DownloadTest.DownloadSizeMb.ShouldBe(ookla.DownloadTest.DownloadSizeMb);
        libreSpeed.DownloadTest.DownloadParallelTasks.ShouldBe(ookla.DownloadTest.DownloadParallelTasks);
        libreSpeed.UploadTest.ShouldBe(ookla.UploadTest);
    }

    [Theory]
    [MemberData(nameof(EveryProfile))]
    public void Profile_ScheduledDownload_ReachesTheCeilingWithoutNeedingToExceedIt(Profile profile)
    {
        // A profile's ceiling is what bounds the traffic, so the scheduled requests must be able
        // to reach it - otherwise the run moves less data than the profile asks for and a fast
        // link is under-measured.

        // Given / When
        var settings = new LibreSpeedtestSettings(profile);

        // Then
        var scheduledMib = (long)settings.DownloadRequestSizeMib * settings.DownloadRequestCount;
        scheduledMib.ShouldBeGreaterThanOrEqualTo(settings.DownloadTest.DownloadSizeMb);

        // And there are enough requests for the profile's connection count to be reached at all.
        settings.DownloadRequestCount.ShouldBeGreaterThanOrEqualTo(settings.DownloadTest.DownloadParallelTasks);
    }

    [Theory]
    [MemberData(nameof(EveryProfile))]
    public void Profile_RequestSize_IsAWholeNumberOfMebibytesGarbageAccepts(Profile profile)
    {
        // garbage.php takes ckSize as a whole number of MiB and clamps it to 1024, so a request
        // size outside that range would be silently answered with something else.

        // Given / When
        var settings = new LibreSpeedtestSettings(profile);

        // Then
        settings.DownloadRequestSizeMib.ShouldBeGreaterThanOrEqualTo(1);
        settings.DownloadRequestSizeMib.ShouldBeLessThanOrEqualTo(1024);
    }

    [Fact]
    public void DefaultConstructor_ResolvesToMedium()
    {
        // Given / When
        var parameterless = new LibreSpeedtestSettings();
        var medium = new LibreSpeedtestSettings(Profile.Medium);

        // Then
        parameterless.ShouldBe(medium);
    }

    [Fact]
    public void UndefinedProfile_IsRejected()
    {
        // Given / When / Then
        Should.Throw<ArgumentOutOfRangeException>(() => new LibreSpeedtestSettings((Profile)999));
    }
}
