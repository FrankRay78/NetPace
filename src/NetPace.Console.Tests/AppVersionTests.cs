namespace NetPace.Console.Tests;

/// <summary>
/// The version NetPace reports about itself: the release it was built as, prerelease suffix included.
/// </summary>
public sealed class AppVersionTests
{
    [Theory]
    [InlineData("0.27.0", "0.27.0")]
    [InlineData("0.27.0-rc1", "0.27.0-rc1")]
    [InlineData("0.27.0-rc.1", "0.27.0-rc.1")]
    public void FromInformationalVersion_ReportsTheReleaseItWasBuiltAs(string informationalVersion, string expected)
    {
        // Given the version a build was stamped with.
        // When it is turned into the version NetPace reports.
        var version = AppVersion.FromInformationalVersion(informationalVersion);

        // Then a prerelease keeps its suffix, which is what tells it apart from the release it precedes.
        Assert.Equal(expected, version);
    }

    [Theory]
    [InlineData("0.27.0+dc6b7c0a1f", "0.27.0")]
    [InlineData("0.27.0-rc1+dc6b7c0a1f", "0.27.0-rc1")]
    public void FromInformationalVersion_WithSourceRevision_LeavesTheRevisionOut(string informationalVersion, string expected)
    {
        // Given a version the SDK has appended the source commit to.
        // When it is turned into the version NetPace reports.
        var version = AppVersion.FromInformationalVersion(informationalVersion);

        // Then the commit is left out: it is build metadata, not part of the release's name.
        Assert.Equal(expected, version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FromInformationalVersion_WithNoVersion_ReportsUnknown(string? informationalVersion)
    {
        // Given an assembly that carries no version.
        // When it is turned into the version NetPace reports.
        var version = AppVersion.FromInformationalVersion(informationalVersion);

        // Then NetPace says it does not know, rather than inventing one.
        Assert.Equal("Unknown", version);
    }
}
