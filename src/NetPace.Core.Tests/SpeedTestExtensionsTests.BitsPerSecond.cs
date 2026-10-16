namespace NetPace.Core.Tests;

public sealed partial class SpeedTestExtensionsTests
{
    public sealed class BitsPerSecond
    {
        [InlineData(0, 1000, 0L)]
        [InlineData(1000, 1000, 8000L)]
        [InlineData(31250, 1000, 250000L)]
        [InlineData(125000, 1000, 1000000L)]
        [InlineData(1000, 8000, 1000L)]
        [InlineData(500, 2000, 2000L)]
        [InlineData(3, 1000, 24L)]
        // A slow link measured in the Kbps range keeps its precision as a whole bit count.
        [InlineData(7000, 3000, 18667L)]
        // Rounding is to the nearest whole bit per second, halves away from zero.
        [InlineData(1, 16000, 1L)]
        [InlineData(1, 32000, 0L)]
        [Theory]
        public void GetBitsPerSecond_Measurement_ReturnsWholeBitsPerSecond(long bytesProcessed, long elapsedMilliseconds, long expected)
        {
            // Given
            var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds, RequestsSucceeded = 1, RequestsFailed = 0 };

            // When
            var bitsPerSecond = result.GetBitsPerSecond();

            // Then
            Assert.Equal(expected, bitsPerSecond);
        }

        [Fact]
        public void GetBitsPerSecond_NoTimeElapsed_ReturnsZero()
        {
            // Given no time elapsed, so no speed was measured.
            var result = new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 0, RequestsSucceeded = 1, RequestsFailed = 0 };

            // When
            var bitsPerSecond = result.GetBitsPerSecond();

            // Then
            Assert.Equal(0L, bitsPerSecond);
        }

        [Fact]
        public void GetBitsPerSecond_AllRequestsFailed_ReturnsZero()
        {
            // Given a test that ran but moved no bytes.
            var result = new SpeedTestResult { BytesProcessed = 0, ElapsedMilliseconds = 2500, RequestsSucceeded = 0, RequestsFailed = 32 };

            // When
            var bitsPerSecond = result.GetBitsPerSecond();

            // Then
            Assert.Equal(0L, bitsPerSecond);
        }

        [InlineData(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, SpeedScale.Auto, "18.67 Kbps")]
        [InlineData(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC, SpeedScale.Mega, "0 MiBps")]
        [InlineData(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI, SpeedScale.Base, "2333.33 Bps")]
        [Theory]
        public void GetBitsPerSecond_WhateverTheFormattingOptions_IsUnchanged(SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale, string expectedFormatted)
        {
            // Given
            var result = new SpeedTestResult { BytesProcessed = 7000, ElapsedMilliseconds = 3000, RequestsSucceeded = 32, RequestsFailed = 0 };

            // When the same measurement is formatted under these options
            var formatted = result.GetSpeedString(unit, unitSystem, scale);

            // Then the options shape the formatted value
            Assert.Equal(expectedFormatted, formatted);

            // And the bits-per-second figure does not depend on them.
            Assert.Equal(18667L, result.GetBitsPerSecond());
        }

        [Fact]
        public void GetBitsPerSecond_Result_Null_ThrowsArgumentNullException()
        {
            // Given
            SpeedTestResult? result = null;

            // When
            var exception = Assert.Throws<ArgumentNullException>(() => result!.GetBitsPerSecond());

            // Then
            Assert.Equal("result", exception.ParamName);
        }
    }
}
