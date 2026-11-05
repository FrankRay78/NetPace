namespace NetPace.Console.Tests;

/// <summary>
/// The live-display refresh a production run gets, as against the stub the test host registers.
/// </summary>
public sealed class ILiveDisplayRefreshTests
{
    /// <summary>
    /// Every console test registers <see cref="NoRefreshStub"/>, so flipping the production value
    /// off would leave the whole suite green while shipping a CLI whose spinner, progress bars and
    /// live speed figure never moved until a test ended. A captured console cannot show a display
    /// moving - that is verified by hand in a real terminal - so the value that decides it is
    /// pinned here instead.
    /// </summary>
    [Fact]
    public void AutoRefresh_ForAProductionRun_IsOn()
    {
        // Given the refresh every production run is registered with
        var production = new LiveDisplayRefresh();

        // When/Then its live displays redraw as a test runs, rather than only once it ends
        Assert.True(production.AutoRefresh);
    }
}
