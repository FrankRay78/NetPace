namespace NetPace.Core.Tests;

/// <summary>
/// Guard clause tests for <see cref="SpeedTestResultExtensions"/>.
/// </summary>
public sealed class SpeedTestResultExtensionsTests
{
    public sealed class Guards
    {
        [Fact]
        public void GetSpeedString_Result_Null_ThrowsArgumentNullException()
        {
            // Given
            SpeedTestResult? result = null;

            // When
            var exception = Assert.Throws<ArgumentNullException>(
                () => result!.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI));

            // Then
            Assert.Equal("result", exception.ParamName);
        }

        [Fact]
        public void GetSpeedString_WithScale_Result_Null_ThrowsArgumentNullException()
        {
            // Given
            SpeedTestResult? result = null;

            // When
            var exception = Assert.Throws<ArgumentNullException>(
                () => result!.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Mega));

            // Then
            Assert.Equal("result", exception.ParamName);
        }

        [Fact]
        public void GetSpeedStringParts_Result_Null_ThrowsArgumentNullException()
        {
            // Given
            SpeedTestResult? result = null;

            // When
            var exception = Assert.Throws<ArgumentNullException>(
                () => result!.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI));

            // Then
            Assert.Equal("result", exception.ParamName);
        }

        [Fact]
        public void GetSpeedStringParts_WithScale_Result_Null_ThrowsArgumentNullException()
        {
            // Given
            SpeedTestResult? result = null;

            // When
            var exception = Assert.Throws<ArgumentNullException>(
                () => result!.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Mega));

            // Then
            Assert.Equal("result", exception.ParamName);
        }

        [Fact]
        public void GetSpeedString_ElapsedMillisecondsZero_ReturnsZero()
        {
            // Given
            var result = new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 0 };

            // When
            var speedString = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then
            Assert.Equal("0 bps", speedString);
        }

        [Fact]
        public void GetSpeedStringParts_ElapsedMillisecondsZero_ReturnsZero()
        {
            // Given
            var result = new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 0 };

            // When
            var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

            // Then
            Assert.Equal("0", speed);
            Assert.Equal("bps", unit);
        }
    }
}
