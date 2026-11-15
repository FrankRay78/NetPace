namespace NetPace.Core.Tests;

/// <summary>
/// A speed is written to three significant figures, so no figure claims more precision than a
/// speed test can measure and no auto-scaled reading is wider than four characters.
/// </summary>
/// <remarks>
/// The bands are read off the figure itself rather than off the unit it landed in: below 10 two
/// decimal places, from 10 to below 100 one, and from 100 up a whole number. Rounding is what makes
/// the bands awkward, because a figure can round out of the band it was measured in and even out of
/// its unit, and a reading written as a bare <c>1000</c> of a unit it had rounded out of would
/// defeat the point of fixing the width.
/// </remarks>
public sealed partial class SpeedTestExtensionsTests
{
    public sealed class SignificantFigures
    {
        private static SpeedTestResult Transferred(long bytesProcessed, long elapsedMilliseconds) =>
            new()
            {
                BytesProcessed = bytesProcessed,
                ElapsedMilliseconds = elapsedMilliseconds,
                RequestsSucceeded = 1,
                RequestsFailed = 0,
                Latency = null
            };

        // Below 10: two decimal places, so a slow link still reads to three figures.
        [InlineData(1000, 1000, "8.00 Kbps")]
        [InlineData(153_750_000, 1000, "1.23 Gbps")]
        // 10 to below 100: one decimal place.
        [InlineData(7000, 3000, "18.7 Kbps")]
        [InlineData(12_345_000, 1000, "98.8 Mbps")]
        // 100 and above: a whole number.
        [InlineData(30_675_000, 1000, "245 Mbps")]
        [Theory]
        public void GetSpeedString_AutoScaledSpeed_ShowsThreeSignificantFigures(long bytesProcessed, long elapsedMilliseconds, string expected)
        {
            // Given a transfer whose speed falls in one of the three bands.
            var result = Transferred(bytesProcessed, elapsedMilliseconds);

            // When it is formatted.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then it carries three significant figures, with the decimal places the band allows.
            Assert.Equal(expected, formatted);
        }

        // 9.996 rounds to 10, which belongs in the next band up: one decimal place, not two.
        [InlineData(2499, 2000, "10.0 Kbps")]
        // 99.96 rounds to 100, which belongs in the band above that: a whole number.
        [InlineData(12_495, 1000, "100 Kbps")]
        [Theory]
        public void GetSpeedString_SpeedThatRoundsIntoTheNextBand_IsShownInThatBand(long bytesProcessed, long elapsedMilliseconds, string expected)
        {
            // Given a transfer whose speed rounds up out of the band it was measured in.
            var result = Transferred(bytesProcessed, elapsedMilliseconds);

            // When it is formatted.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then the figure is written in the band it rounded into, so it still shows three
            // significant figures rather than four.
            Assert.Equal(expected, formatted);
        }

        [Fact]
        public void GetSpeedString_SpeedThatRoundsUpToTheSiDivisor_IsShownInTheNextUnit()
        {
            // SCENARIO: Rounding moves a speed to the next unit rather than showing 1000

            // Given 999.6 Kbps, which a whole-number band would round to 1000 of its own unit.
            var result = Transferred(124_950, 1000);

            // When it is formatted under SI auto-scaling.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then it steps up a unit rather than reading 1000 Kbps, which no auto-scaled speed
            // should ever show.
            Assert.Equal("1.00 Mbps", formatted);
        }

        [Fact]
        public void GetSpeedString_IecSpeedBelowItsDivisor_IsShownAsAFourDigitWholeNumber()
        {
            // Given 1010 Kibps, which IEC auto-scaling leaves in Kibps because its divisor is 1024.
            var result = Transferred(129_280, 1000);

            // When it is formatted.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC);

            // Then it reads as a whole number of four digits: the figure is what the band is read
            // from, and it is above 100.
            Assert.Equal("1010 Kibps", formatted);
        }

        [Fact]
        public void GetSpeedString_SpeedThatRoundsUpToTheIecDivisor_IsShownInTheNextUnit()
        {
            // Given 1023.7 Kibps, which a whole-number band would round to 1024 of its own unit.
            var result = Transferred(131_034, 1000);

            // When it is formatted under IEC auto-scaling.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC);

            // Then it steps up a unit, the same rule SI follows at 1000: one rule, each unit system
            // measured against its own divisor.
            Assert.Equal("1.00 Mibps", formatted);
        }

        [InlineData(15_625_000, SpeedScale.Kilo, "125000 Kbps")]
        [InlineData(30_675_000, SpeedScale.Kilo, "245400 Kbps")]
        [Theory]
        public void GetSpeedString_PinnedScale_KeepsEveryDigitBeforeThePoint(long bytesProcessed, SpeedScale scale, string expected)
        {
            // Given a speed pinned to a scale small enough that the figure runs to six digits.
            var result = Transferred(bytesProcessed, 1000);

            // When it is formatted at that scale.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, scale);

            // Then no digit before the decimal point is rounded away: three significant figures is
            // a floor on precision, never a cap on the figure's size.
            Assert.Equal(expected, formatted);
        }

        [InlineData(SpeedScale.Auto, "0 bps")]
        [InlineData(SpeedScale.Base, "0 bps")]
        [InlineData(SpeedScale.Mega, "0 Mbps")]
        [Theory]
        public void GetSpeedString_ZeroSpeed_IsShownAsABareZero(SpeedScale scale, string expected)
        {
            // Given a transfer that moved no bytes, which is how an all-failed transfer reports.
            var result = Transferred(0, 1000);

            // When it is formatted.
            var formatted = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, scale);

            // Then it reads as a bare zero rather than 0.00: there is no measurement to show three
            // figures of.
            Assert.Equal(expected, formatted);
        }

        [Fact]
        public void GetSpeedStringParts_AutoScaledSpeed_SplitsTheSameTextTheWholeStringCarries()
        {
            // Given a speed whose band gives it two decimal places.
            var result = Transferred(1000, 1000);

            // When it is formatted both ways.
            var whole = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);
            var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then the parts are the whole string split at the space, so a caller assembling them
            // cannot read a different figure from a caller taking the string.
            Assert.Equal("8.00", speed);
            Assert.Equal("Kbps", unit);
            Assert.Equal(whole, $"{speed} {unit}");
        }

        [Fact]
        public void GetSpeedString_Progress_ShowsTheSameBandsAsAResult()
        {
            // Given a running transfer at the same speed as a finished one.
            var progress = new SpeedTestProgress { BytesProcessed = 7000, ElapsedMilliseconds = 3000 };

            // When the live figure is formatted.
            var formatted = progress.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then it is written to the same band, so the figure a user watches climb does not
            // change precision when the measurement settles.
            Assert.Equal("18.7 Kbps", formatted);
        }
    }
}
