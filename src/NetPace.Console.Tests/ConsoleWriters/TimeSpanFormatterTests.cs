using NetPace.Console.ConsoleWriters;
using Xunit;

namespace NetPace.Console.Tests.ConsoleWriters;

public class TimeSpanFormatterTests
{
    // Replacement formatter mirrors the prior Humanizer(precision:1) contract: largest non-zero
    // unit, truncated (never rounded). Zero/negative spans are coerced defensively to "0 seconds"
    // (test-plan FR-011 contract).

    [Fact]
    public void Humanize_OneSecond_ReturnsSingularForm()
    {
        // SCENARIO: Replacement formatter produces "1 second" for one-second TimeSpan

        Assert.Equal("1 second", TimeSpan.FromSeconds(1).Humanize());
    }

    [Fact]
    public void Humanize_MultipleSeconds_ReturnsPluralForm()
    {
        // SCENARIO: Replacement formatter pluralises for multi-second TimeSpan

        Assert.Equal("7 seconds", TimeSpan.FromSeconds(7).Humanize());
    }

    [Fact]
    public void Humanize_FractionalSeconds_TruncatesToWholeSeconds()
    {
        // SCENARIO: Replacement formatter truncates fractional seconds to whole seconds

        Assert.Equal("2 seconds", TimeSpan.FromMilliseconds(2400).Humanize());
    }

    [Fact]
    public void Humanize_ZeroTimeSpan_ReturnsZeroSecondsString()
    {
        // SCENARIO: Replacement formatter handles zero TimeSpan defensively

        Assert.Equal("0 seconds", TimeSpan.Zero.Humanize());
    }

    // Full unit-ladder coverage: Year → Month → Week → Day → Hour → Minute → Second → Millisecond.

    [Theory]
    [InlineData(1, "1 millisecond")]
    [InlineData(500, "500 milliseconds")]
    [InlineData(999, "999 milliseconds")]
    public void Humanize_Milliseconds_FormatsAsMilliseconds(int ms, string expected) =>
        Assert.Equal(expected, TimeSpan.FromMilliseconds(ms).Humanize());

    [Theory]
    [InlineData(1, "1 second")]
    [InlineData(2, "2 seconds")]
    [InlineData(59, "59 seconds")]
    public void Humanize_Seconds_FormatsAsSeconds(int s, string expected) =>
        Assert.Equal(expected, TimeSpan.FromSeconds(s).Humanize());

    [Theory]
    [InlineData(2400, "2 seconds")]   // 2.4s — truncates to 2 (also happens to match rounding)
    [InlineData(2600, "2 seconds")]   // 2.6s — truncates to 2 (NOT 3; Humanizer never rounds)
    [InlineData(2999, "2 seconds")]   // just under 3s — still 2
    public void Humanize_FractionalSeconds_TruncatesDoesNotRound(int ms, string expected) =>
        Assert.Equal(expected, TimeSpan.FromMilliseconds(ms).Humanize());

    [Theory]
    [InlineData(1, "1 minute")]
    [InlineData(5, "5 minutes")]
    [InlineData(59, "59 minutes")]
    public void Humanize_Minutes_FormatsAsMinutes(int min, string expected) =>
        Assert.Equal(expected, TimeSpan.FromMinutes(min).Humanize());

    [Theory]
    [InlineData(1, "1 hour")]
    [InlineData(2, "2 hours")]
    [InlineData(23, "23 hours")]
    public void Humanize_Hours_FormatsAsHours(int h, string expected) =>
        Assert.Equal(expected, TimeSpan.FromHours(h).Humanize());

    [Fact]
    public void Humanize_OneAndAHalfHours_TruncatesToOneHour() =>
        // 1.5h truncates (not rounds) to 1 hour — Humanizer parity.
        Assert.Equal("1 hour", TimeSpan.FromMinutes(90).Humanize());

    [Theory]
    [InlineData(1, "1 day")]
    [InlineData(2, "2 days")]
    [InlineData(6, "6 days")]
    public void Humanize_Days_FormatsAsDays(int d, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(d).Humanize());

    [Theory]
    [InlineData(7, "1 week")]
    [InlineData(14, "2 weeks")]
    [InlineData(28, "4 weeks")]
    public void Humanize_Weeks_FormatsAsWeeks(int days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).Humanize());

    [Theory]
    [InlineData(31, "1 month")]    // 31d / 30.436875 = 1.018 → 1 month
    [InlineData(60, "1 month")]    // 60d / 30.436875 = 1.971 → 1 month (still under 2)
    [InlineData(61, "2 months")]   // 61d / 30.436875 = 2.004 → 2 months (just over)
    [InlineData(91, "2 months")]   // 91d / 30.436875 = 2.99  → 2 months
    public void Humanize_Months_FormatsAsMonths(int days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).Humanize());

    [Theory]
    [InlineData(366, "1 year")]    // 366d / 365.2425 = 1.002 → 1 year
    [InlineData(731, "2 years")]   // 731d / 365.2425 = 2.001 → 2 years
    public void Humanize_Years_FormatsAsYears(int days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).Humanize());

    // Boundary crossings — value just below the next unit's threshold should
    // still report the lower unit; value at the threshold should report the higher.

    [Theory]
    [InlineData(1_000, "1 second")]                  // ms → s
    [InlineData(60_000, "1 minute")]                 // s  → m
    [InlineData(60 * 60 * 1_000, "1 hour")]          // m  → h
    [InlineData(24 * 60 * 60 * 1_000, "1 day")]      // h  → d
    public void Humanize_BoundaryCrossings_PromoteToNextUnit(int ms, string expected) =>
        Assert.Equal(expected, TimeSpan.FromMilliseconds(ms).Humanize());

    [Theory]
    [InlineData(999, "999 milliseconds")]            // just under 1s — still ms
    [InlineData(59_999, "59 seconds")]               // just under 1m — still s
    public void Humanize_JustBelowBoundary_StaysOnLowerUnit(int ms, string expected) =>
        Assert.Equal(expected, TimeSpan.FromMilliseconds(ms).Humanize());

    [Theory]
    [InlineData(365, "11 months")]                   // 365d < 1y (365.2425), falls back to months: 11.99 → 11
    public void Humanize_JustUnderOneYear_FallsBackToMonths(int days, string expected) =>
        Assert.Equal(expected, TimeSpan.FromDays(days).Humanize());

    // Defensive cases.

    [Fact]
    public void Humanize_NegativeTimeSpan_ReturnsZeroSecondsDefensively() =>
        Assert.Equal("0 seconds", TimeSpan.FromSeconds(-5).Humanize());
}
