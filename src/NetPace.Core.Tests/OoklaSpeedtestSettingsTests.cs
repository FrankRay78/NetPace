using NetPace.Core.Clients.Ookla;
using Shouldly;

namespace NetPace.Core.Tests;

/// <summary>
/// Cross-story invariant tests for <see cref="OoklaSpeedtestSettings"/>'s parameterless
/// and <see cref="Profile"/>-taking constructors.
/// </summary>
public sealed partial class OoklaSpeedtestSettingsTests
{
    [Fact]
    public void ParameterlessCtor_EqualsMediumProfileCtor()
    {
        // SCENARIO: Parameterless ctor chains to Medium

        // Given / When
        var parameterless = new OoklaSpeedtestSettings();
        var medium = new OoklaSpeedtestSettings(Profile.Medium);

        // Then — assert field-for-field equality (int[] DownloadSizes doesn't have
        // structural equality, so record-level .Equals would compare arrays by reference).
        parameterless.DownloadTest.DownloadSizes.ShouldBe(medium.DownloadTest.DownloadSizes);
        parameterless.DownloadTest.DownloadSizeIterations.ShouldBe(medium.DownloadTest.DownloadSizeIterations);
        parameterless.DownloadTest.DownloadParallelTasks.ShouldBe(medium.DownloadTest.DownloadParallelTasks);
        parameterless.DownloadTest.DownloadSizeMb.ShouldBe(medium.DownloadTest.DownloadSizeMb);

        parameterless.UploadTest.UploadSizeIncrementKb.ShouldBe(medium.UploadTest.UploadSizeIncrementKb);
        parameterless.UploadTest.UploadIncrements.ShouldBe(medium.UploadTest.UploadIncrements);
        parameterless.UploadTest.UploadSizeIterations.ShouldBe(medium.UploadTest.UploadSizeIterations);
        parameterless.UploadTest.UploadParallelTasks.ShouldBe(medium.UploadTest.UploadParallelTasks);
        parameterless.UploadTest.UploadSizeMb.ShouldBe(medium.UploadTest.UploadSizeMb);

        // Spot-check Medium field values so any value drift surfaces here too.
        parameterless.DownloadTest.DownloadSizes.ShouldBe(new[] { 1500, 2000, 3000, 3500, 4000 });
        parameterless.DownloadTest.DownloadSizeIterations.ShouldBe(2);
        parameterless.DownloadTest.DownloadParallelTasks.ShouldBe(4);
        parameterless.DownloadTest.DownloadSizeMb.ShouldBe(100);

        parameterless.UploadTest.UploadSizeIncrementKb.ShouldBe(200);
        parameterless.UploadTest.UploadIncrements.ShouldBe(6);
        parameterless.UploadTest.UploadSizeIterations.ShouldBe(5);
        parameterless.UploadTest.UploadParallelTasks.ShouldBe(4);
        parameterless.UploadTest.UploadSizeMb.ShouldBe(25);
    }

    [Fact]
    public void Ctor_UnknownProfile_ThrowsArgumentOutOfRangeException()
    {
        // SCENARIO: Construct invalid profile throws

        // Given
        var bogus = (Profile)999;

        // When
        var ex = Should.Throw<ArgumentOutOfRangeException>(() => new OoklaSpeedtestSettings(bogus));

        // Then
        ex.ParamName.ShouldBe("profile");
    }

    [Fact]
    public void WithExpression_PreservesProfileFields_AndAppliesOverride()
    {
        // SCENARIO: `with` expression composes cleanly on profile-built record

        // Given
        var s = new OoklaSpeedtestSettings(Profile.Mega) with { UseProxy = true };

        // Then
        s.UseProxy.ShouldBeTrue();

        // Mega's per-request shape is preserved.
        s.DownloadTest.DownloadSizes.ShouldContain(5000);
        s.DownloadTest.DownloadSizes.ShouldContain(6000);
        s.DownloadTest.DownloadSizes.ShouldContain(7000);
        s.DownloadTest.DownloadParallelTasks.ShouldBe(32);
        s.UploadTest.UploadSizeIncrementKb.ShouldBe(1024);
    }

    [Fact]
    public void OoklaSpeedtestSettings_HasNoProfileProperty()
    {
        // Reflection guard: profile is consumed by the ctor but never stored as state.
        var profileProp = typeof(OoklaSpeedtestSettings).GetProperty("Profile");

        profileProp.ShouldBeNull();
    }
}
