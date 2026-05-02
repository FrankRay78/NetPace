using NetPace.Console.ConsoleWriters;
using Xunit;

namespace NetPace.Console.Tests.ConsoleWriters;

public class TimeSpanFormatterTests
{
    [Fact]
    public void Humanize_OneSecond_ReturnsSingularForm()
    {
        // SCENARIO: Replacement formatter produces "1 second" for one-second TimeSpan

        var actual = TimeSpan.FromSeconds(1).Humanize();

        Assert.Equal("1 second", actual);
    }

    [Fact]
    public void Humanize_MultipleSeconds_ReturnsPluralForm()
    {
        // SCENARIO: Replacement formatter pluralises for multi-second TimeSpan

        var actual = TimeSpan.FromSeconds(7).Humanize();

        Assert.Equal("7 seconds", actual);
    }

    [Fact]
    public void Humanize_FractionalSeconds_RoundsToWholeSeconds()
    {
        // SCENARIO: Replacement formatter rounds fractional seconds to whole seconds

        var actual = TimeSpan.FromMilliseconds(2400).Humanize();

        Assert.Equal("2 seconds", actual);
    }

    [Fact]
    public void Humanize_ZeroTimeSpan_ReturnsZeroSecondsString()
    {
        // SCENARIO: Replacement formatter handles zero TimeSpan defensively

        var actual = TimeSpan.Zero.Humanize();

        Assert.Equal("0 seconds", actual);
    }
}
