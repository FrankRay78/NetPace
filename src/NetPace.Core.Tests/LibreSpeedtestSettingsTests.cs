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
        // The scheduled requests must be able to reach the profile's ceiling, or a fast link is
        // under-measured.

        // Given / When
        var settings = new LibreSpeedtestSettings(profile);

        // Then
        var scheduledMib = (long)settings.DownloadRequestSizeMib * settings.DownloadRequestCount;
        scheduledMib.ShouldBeGreaterThanOrEqualTo(settings.DownloadTest.DownloadSizeMb);

        // And no single request is larger than the ceiling, so reaching it never needs one
        // response to carry the run past it.
        settings.DownloadRequestSizeMib.ShouldBeLessThanOrEqualTo(settings.DownloadTest.DownloadSizeMb);

        // And there are enough requests for the profile's connection count to be reached at all.
        settings.DownloadRequestCount.ShouldBeGreaterThanOrEqualTo(settings.DownloadTest.DownloadParallelTasks);
    }

    [Theory]
    [MemberData(nameof(EveryProfile))]
    public void Profile_RequestSize_IsAWholeNumberOfMebibytesGarbageAccepts(Profile profile)
    {
        // garbage.php answers a ckSize of zero or less with its own default and caps it at 1024,
        // so a size outside that range is silently answered with another.

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
