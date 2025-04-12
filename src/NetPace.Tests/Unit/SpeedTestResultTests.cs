namespace NetPace.Tests.Unit;

public class SpeedTestResultTests
{
    [InlineData(0, 1000, "0 Bps")]
    [InlineData(1, 1000, "1 Bps")]
    [InlineData(999, 1000, "999 Bps")]
    [InlineData(1000, 1000, "1 KBps")]
    [InlineData(1500, 1000, "1.5 KBps")]
    [InlineData(10000, 1000, "10 KBps")]
    [InlineData(1000000, 1000, "1 MBps")]
    [InlineData(1500000, 1000, "1.5 MBps")]
    [InlineData(1000000000, 1000, "1 GBps")]
    [InlineData(1000000000000, 1000, "1 TBps")]
    [InlineData(1000000000000000, 1000, "1 PBps")]
    // Adjust the milliseconds to test fractional rounding
    [InlineData(1, 2000, "0.5 Bps")]
    [InlineData(1, 4000, "0.25 Bps")]
    [InlineData(1, 8000, "0.13 Bps")]
    [InlineData(1, 16000, "0.06 Bps")]
    [InlineData(1, 32000, "0.03 Bps")]
    [InlineData(1, 64000, "0.02 Bps")]
    [InlineData(500, 2000, "250 Bps")]
    // Values near the transition point between KB and MB, 
    // ensuring correctness up to two decimal places.
    [InlineData(999994, 1000, "999.99 KBps")]
    [InlineData(999995, 1000, "1 MBps")]
    [Theory]
    public void Should_Calculate_Bytes_Per_Second_Correctly_SI(long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI);

        // Then
        Assert.Equal(expected, speedString);
    }

    [InlineData(0, 1000, "0 Bps")]
    [InlineData(1, 1000, "1 Bps")]
    [InlineData(1023, 1000, "1023 Bps")]
    [InlineData(1024, 1000, "1 KiBps")]
    [InlineData(1536, 1000, "1.5 KiBps")]
    [InlineData(10240, 1000, "10 KiBps")]
    [InlineData(1048576, 1000, "1 MiBps")]
    [InlineData(1572864, 1000, "1.5 MiBps")]
    [InlineData(1073741824, 1000, "1 GiBps")]
    [InlineData(1099511627776, 1000, "1 TiBps")]
    [InlineData(1125899906842624, 1000, "1 PiBps")]
    // Adjust the milliseconds to test fractional rounding
    [InlineData(1, 2000, "0.5 Bps")]
    [InlineData(1, 4000, "0.25 Bps")]
    [InlineData(1, 8000, "0.13 Bps")]
    [InlineData(1, 16000, "0.06 Bps")]
    [InlineData(1, 32000, "0.03 Bps")]
    [InlineData(1, 64000, "0.02 Bps")]
    [InlineData(512, 2000, "256 Bps")]
    // Values near the transition point between KB and MB, 
    // ensuring correctness up to two decimal places.
    [InlineData(1048570, 1000, "1023.99 KiBps")]
    [InlineData(1048571, 1000, "1 MiBps")]
    [Theory]
    public void Should_Calculate_Bytes_Per_Second_Correctly_IEC(long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC);

        // Then
        Assert.Equal(expected, speedString);
    }

    [InlineData(0, 1000, "0 bps")]
    [InlineData(1, 1000, "8 bps")]
    [InlineData(999, 1000, "7.99 Kbps")]
    [InlineData(1000, 1000, "8 Kbps")]
    [InlineData(1500, 1000, "12 Kbps")]
    [InlineData(10000, 1000, "80 Kbps")]
    [InlineData(1000000, 1000, "8 Mbps")]
    [InlineData(1500000, 1000, "12 Mbps")]
    [InlineData(1000000000, 1000, "8 Gbps")]
    [InlineData(1000000000000, 1000, "8 Tbps")]
    [InlineData(1000000000000000, 1000, "8 Pbps")]
    // Adjust the milliseconds to test fractional rounding
    [InlineData(1, 2000, "4 bps")]
    [InlineData(1, 4000, "2 bps")]
    [InlineData(1, 8000, "1 bps")]
    [InlineData(1, 16000, "0.5 bps")]
    [InlineData(1, 32000, "0.25 bps")]
    [InlineData(1, 64000, "0.13 bps")]
    [InlineData(500, 2000, "2 Kbps")]
    [Theory]
    public void Should_Calculate_Bits_Per_Second_Correctly_SI(long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

        // Then
        Assert.Equal(expected, speedString);
    }

    [InlineData(0, 1000, "0 bps")]
    [InlineData(1, 1000, "8 bps")]
    [InlineData(1023, 1000, "7.99 Kibps")]
    [InlineData(1024, 1000, "8 Kibps")]
    [InlineData(1536, 1000, "12 Kibps")]
    [InlineData(10240, 1000, "80 Kibps")]
    [InlineData(1048576, 1000, "8 Mibps")]
    [InlineData(1572864, 1000, "12 Mibps")]
    [InlineData(1073741824, 1000, "8 Gibps")]
    [InlineData(1099511627776, 1000, "8 Tibps")]
    [InlineData(1125899906842624, 1000, "8 Pibps")]
    // Adjust the milliseconds to test fractional rounding
    [InlineData(1, 2000, "4 bps")]
    [InlineData(1, 4000, "2 bps")]
    [InlineData(1, 8000, "1 bps")]
    [InlineData(1, 16000, "0.5 bps")]
    [InlineData(1, 32000, "0.25 bps")]
    [InlineData(1, 64000, "0.13 bps")]
    [InlineData(512, 2000, "2 Kibps")]
    [Theory]
    public void Should_Calculate_Bits_Per_Second_Correctly_IEC(long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC);

        // Then
        Assert.Equal(expected, speedString);
    }
}
