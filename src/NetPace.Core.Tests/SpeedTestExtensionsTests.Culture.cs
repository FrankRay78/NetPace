using System.Globalization;

namespace NetPace.Core.Tests;

/// <summary>
/// Speed text is the same whatever culture the calling thread carries, so a library consumer and
/// every output mode built on one read the same figure on every machine.
/// </summary>
public sealed partial class SpeedTestExtensionsTests
{
    public sealed class Culture
    {
        /// <summary>
        /// 7000 bytes in 3 seconds is 18666.67 bits per second, which renders as the fractional
        /// "18.67 Kbps" — a value whose decimal separator a comma-decimal culture would change.
        /// </summary>
        private static SpeedTestResult FractionalSpeed() =>
            new() { BytesProcessed = 7000, ElapsedMilliseconds = 3000, RequestsSucceeded = 1, RequestsFailed = 0 };

        /// <summary>
        /// Runs <paramref name="format"/> on a thread set to the named culture and hands back what
        /// it produced, restoring the original culture first. Only the formatting runs under the
        /// swapped culture: the assertions are left to the caller, so a failure is reported under
        /// the culture the test runner started in.
        /// </summary>
        private static T UnderCulture<T>(string name, Func<T> format)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(name);
                return format();
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void GetSpeedString_CommaDecimalCulture_UsesFullStopDecimalSeparator(string culture)
        {
            // SCENARIO: Library speed formatting ignores the current culture

            // Given a result whose speed has a fractional part.
            var result = FractionalSpeed();

            // When it is formatted on a thread whose culture is not English.
            var formatted = UnderCulture(culture, () => result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI));

            // Then the text reads the same as it does under an English culture.
            Assert.Equal("18.67 Kbps", formatted);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void GetSpeedStringParts_CommaDecimalCulture_UsesFullStopDecimalSeparator(string culture)
        {
            // Given
            var result = FractionalSpeed();

            // When
            var (speed, unit) = UnderCulture(culture, () => result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI));

            // Then
            Assert.Equal("18.67", speed);
            Assert.Equal("Kbps", unit);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void GetSpeedString_FixedScale_CommaDecimalCulture_UsesFullStopDecimalSeparator(string culture)
        {
            // Given a fixed scale, which formats down a separate path from auto-scaling.
            var result = FractionalSpeed();

            // When
            var formatted = UnderCulture(culture, () => result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Kilo));

            // Then
            Assert.Equal("18.67 Kbps", formatted);
        }

        [Theory]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void GetSpeedString_Progress_CommaDecimalCulture_UsesFullStopDecimalSeparator(string culture)
        {
            // Given the progress overload, which the live display formats through.
            var progress = new SpeedTestProgress { BytesProcessed = 7000, ElapsedMilliseconds = 3000 };

            // When
            var formatted = UnderCulture(culture, () => progress.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI));

            // Then
            Assert.Equal("18.67 Kbps", formatted);
        }
    }
}
