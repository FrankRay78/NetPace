namespace NetPace.Core.Tests;

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
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
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
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
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
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
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
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
    }

    /// <summary>
    /// Test fixed scale functionality with SI units, focusing on fractional calculations
    /// and ensuring consistency across all scale levels.
    /// </summary>
    [InlineData(SpeedScale.Base, 0, 1000, "0 Bps")]
    [InlineData(SpeedScale.Base, 1, 1000, "1 Bps")]
    [InlineData(SpeedScale.Base, 500, 1000, "500 Bps")]
    [InlineData(SpeedScale.Base, 1000, 1000, "1000 Bps")]
    [InlineData(SpeedScale.Base, 1500, 1000, "1500 Bps")]
    [InlineData(SpeedScale.Base, 999999, 1000, "999999 Bps")]
    [InlineData(SpeedScale.Base, 1000000, 1000, "1000000 Bps")]
    // Kilo scale tests with fractional calculations
    [InlineData(SpeedScale.Kilo, 0, 1000, "0 KBps")]
    [InlineData(SpeedScale.Kilo, 500, 1000, "0.5 KBps")]
    [InlineData(SpeedScale.Kilo, 1000, 1000, "1 KBps")]
    [InlineData(SpeedScale.Kilo, 1250, 1000, "1.25 KBps")]
    [InlineData(SpeedScale.Kilo, 1333, 1000, "1.33 KBps")]
    [InlineData(SpeedScale.Kilo, 1666, 1000, "1.67 KBps")]
    [InlineData(SpeedScale.Kilo, 10000, 1000, "10 KBps")]
    [InlineData(SpeedScale.Kilo, 500000, 1000, "500 KBps")]
    [InlineData(SpeedScale.Kilo, 999999, 1000, "1000 KBps")]
    // Mega scale tests with small fractional values
    [InlineData(SpeedScale.Mega, 1000, 1000, "0 MBps")]
    [InlineData(SpeedScale.Mega, 5000, 1000, "0.01 MBps")]
    [InlineData(SpeedScale.Mega, 10000, 1000, "0.01 MBps")]
    [InlineData(SpeedScale.Mega, 50000, 1000, "0.05 MBps")]
    [InlineData(SpeedScale.Mega, 100000, 1000, "0.1 MBps")]
    [InlineData(SpeedScale.Mega, 250000, 1000, "0.25 MBps")]
    [InlineData(SpeedScale.Mega, 500000, 1000, "0.5 MBps")]
    [InlineData(SpeedScale.Mega, 750000, 1000, "0.75 MBps")]
    [InlineData(SpeedScale.Mega, 1000000, 1000, "1 MBps")]
    [InlineData(SpeedScale.Mega, 1250000, 1000, "1.25 MBps")]
    [InlineData(SpeedScale.Mega, 1333333, 1000, "1.33 MBps")]
    [InlineData(SpeedScale.Mega, 2500000, 1000, "2.5 MBps")]
    // Giga scale tests with very small fractional values
    [InlineData(SpeedScale.Giga, 1000000, 1000, "0 GBps")]
    [InlineData(SpeedScale.Giga, 10000000, 1000, "0.01 GBps")]
    [InlineData(SpeedScale.Giga, 50000000, 1000, "0.05 GBps")]
    [InlineData(SpeedScale.Giga, 100000000, 1000, "0.1 GBps")]
    [InlineData(SpeedScale.Giga, 500000000, 1000, "0.5 GBps")]
    [InlineData(SpeedScale.Giga, 1000000000, 1000, "1 GBps")]
    [InlineData(SpeedScale.Giga, 1250000000, 1000, "1.25 GBps")]
    // Tera scale tests
    [InlineData(SpeedScale.Tera, 1000000000, 1000, "0 TBps")]
    [InlineData(SpeedScale.Tera, 100000000000, 1000, "0.1 TBps")]
    [InlineData(SpeedScale.Tera, 500000000000, 1000, "0.5 TBps")]
    [InlineData(SpeedScale.Tera, 1000000000000, 1000, "1 TBps")]
    // Peta scale tests
    [InlineData(SpeedScale.Peta, 1000000000000, 1000, "0 PBps")]
    [InlineData(SpeedScale.Peta, 100000000000000, 1000, "0.1 PBps")]
    [InlineData(SpeedScale.Peta, 1000000000000000, 1000, "1 PBps")]
    // Time-based fractional calculations
    [InlineData(SpeedScale.Kilo, 1000, 2000, "0.5 KBps")]
    [InlineData(SpeedScale.Kilo, 1000, 4000, "0.25 KBps")]
    [InlineData(SpeedScale.Kilo, 1000, 8000, "0.13 KBps")]
    [InlineData(SpeedScale.Mega, 1000000, 3000, "0.33 MBps")]
    [InlineData(SpeedScale.Mega, 2000000, 3000, "0.67 MBps")]
    [Theory]
    public void Should_Calculate_Bytes_Per_Second_With_Fixed_Scale_SI(SpeedScale scale, long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI, scale);
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BytesPerSecond, SpeedUnitSystem.SI, scale);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
    }

    /// <summary>
    /// Test fixed scale functionality with IEC units, focusing on fractional calculations
    /// using powers of 1024 and ensuring binary precision.
    /// </summary>
    [InlineData(SpeedScale.Base, 0, 1000, "0 Bps")]
    [InlineData(SpeedScale.Base, 1, 1000, "1 Bps")]
    [InlineData(SpeedScale.Base, 512, 1000, "512 Bps")]
    [InlineData(SpeedScale.Base, 1024, 1000, "1024 Bps")]
    [InlineData(SpeedScale.Base, 1536, 1000, "1536 Bps")]
    [InlineData(SpeedScale.Base, 1048575, 1000, "1048575 Bps")]
    [InlineData(SpeedScale.Base, 1048576, 1000, "1048576 Bps")]
    // Kibi scale tests with fractional calculations
    [InlineData(SpeedScale.Kilo, 0, 1000, "0 KiBps")]
    [InlineData(SpeedScale.Kilo, 512, 1000, "0.5 KiBps")]
    [InlineData(SpeedScale.Kilo, 1024, 1000, "1 KiBps")]
    [InlineData(SpeedScale.Kilo, 1280, 1000, "1.25 KiBps")]
    [InlineData(SpeedScale.Kilo, 1365, 1000, "1.33 KiBps")]
    [InlineData(SpeedScale.Kilo, 1536, 1000, "1.5 KiBps")]
    [InlineData(SpeedScale.Kilo, 1707, 1000, "1.67 KiBps")]
    [InlineData(SpeedScale.Kilo, 2048, 1000, "2 KiBps")]
    [InlineData(SpeedScale.Kilo, 10240, 1000, "10 KiBps")]
    [InlineData(SpeedScale.Kilo, 512000, 1000, "500 KiBps")]
    [InlineData(SpeedScale.Kilo, 1048576, 1000, "1024 KiBps")]
    // Mebi scale tests with small fractional values
    [InlineData(SpeedScale.Mega, 1024, 1000, "0 MiBps")]
    [InlineData(SpeedScale.Mega, 5120, 1000, "0 MiBps")]
    [InlineData(SpeedScale.Mega, 10485, 1000, "0.01 MiBps")]
    [InlineData(SpeedScale.Mega, 52428, 1000, "0.05 MiBps")]
    [InlineData(SpeedScale.Mega, 104857, 1000, "0.1 MiBps")]
    [InlineData(SpeedScale.Mega, 262144, 1000, "0.25 MiBps")]
    [InlineData(SpeedScale.Mega, 524288, 1000, "0.5 MiBps")]
    [InlineData(SpeedScale.Mega, 786432, 1000, "0.75 MiBps")]
    [InlineData(SpeedScale.Mega, 1048576, 1000, "1 MiBps")]
    [InlineData(SpeedScale.Mega, 1310720, 1000, "1.25 MiBps")]
    [InlineData(SpeedScale.Mega, 1398101, 1000, "1.33 MiBps")]
    [InlineData(SpeedScale.Mega, 1572864, 1000, "1.5 MiBps")]
    [InlineData(SpeedScale.Mega, 2621440, 1000, "2.5 MiBps")]
    // Gibi scale tests with very small fractional values
    [InlineData(SpeedScale.Giga, 1048576, 1000, "0 GiBps")]
    [InlineData(SpeedScale.Giga, 10737418, 1000, "0.01 GiBps")]
    [InlineData(SpeedScale.Giga, 53687091, 1000, "0.05 GiBps")]
    [InlineData(SpeedScale.Giga, 107374182, 1000, "0.1 GiBps")]
    [InlineData(SpeedScale.Giga, 536870912, 1000, "0.5 GiBps")]
    [InlineData(SpeedScale.Giga, 1073741824, 1000, "1 GiBps")]
    [InlineData(SpeedScale.Giga, 1342177280, 1000, "1.25 GiBps")]
    // Tebi scale tests
    [InlineData(SpeedScale.Tera, 1073741824, 1000, "0 TiBps")]
    [InlineData(SpeedScale.Tera, 109951162777, 1000, "0.1 TiBps")]
    [InlineData(SpeedScale.Tera, 549755813888, 1000, "0.5 TiBps")]
    [InlineData(SpeedScale.Tera, 1099511627776, 1000, "1 TiBps")]
    // Pebi scale tests
    [InlineData(SpeedScale.Peta, 1099511627776, 1000, "0 PiBps")]
    [InlineData(SpeedScale.Peta, 112589990684262, 1000, "0.1 PiBps")]
    [InlineData(SpeedScale.Peta, 1125899906842624, 1000, "1 PiBps")]
    // Time-based fractional calculations with binary precision
    [InlineData(SpeedScale.Kilo, 1024, 2000, "0.5 KiBps")]
    [InlineData(SpeedScale.Kilo, 1024, 4000, "0.25 KiBps")]
    [InlineData(SpeedScale.Kilo, 1024, 8000, "0.13 KiBps")]
    [InlineData(SpeedScale.Mega, 1048576, 3000, "0.33 MiBps")]
    [InlineData(SpeedScale.Mega, 2097152, 3000, "0.67 MiBps")]
    // Edge cases around fractional rounding
    [InlineData(SpeedScale.Kilo, 1023, 1000, "1 KiBps")]
    [InlineData(SpeedScale.Mega, 1048575, 1000, "1 MiBps")]
    [InlineData(SpeedScale.Mega, 1048570, 1000, "1 MiBps")]
    [Theory]
    public void Should_Calculate_Bytes_Per_Second_With_Fixed_Scale_IEC(SpeedScale scale, long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC, scale);
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BytesPerSecond, SpeedUnitSystem.IEC, scale);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
    }

    /// <summary>
    /// Test fixed scale functionality for bits per second with SI units, focusing on fractional calculations
    /// and the 8x multiplier effect on precision.
    /// </summary>
    [InlineData(SpeedScale.Base, 0, 1000, "0 bps")]
    [InlineData(SpeedScale.Base, 1, 1000, "8 bps")]
    [InlineData(SpeedScale.Base, 125, 1000, "1000 bps")]
    [InlineData(SpeedScale.Base, 250, 1000, "2000 bps")]
    [InlineData(SpeedScale.Base, 500, 1000, "4000 bps")]
    [InlineData(SpeedScale.Base, 625, 1000, "5000 bps")]
    [InlineData(SpeedScale.Base, 1000, 1000, "8000 bps")]
    [InlineData(SpeedScale.Base, 125000, 1000, "1000000 bps")]
    // Kilo scale tests with fractional calculations from bit conversion
    [InlineData(SpeedScale.Kilo, 0, 1000, "0 Kbps")]
    [InlineData(SpeedScale.Kilo, 62, 1000, "0.5 Kbps")]
    [InlineData(SpeedScale.Kilo, 63, 1000, "0.5 Kbps")]
    [InlineData(SpeedScale.Kilo, 125, 1000, "1 Kbps")]
    [InlineData(SpeedScale.Kilo, 156, 1000, "1.25 Kbps")]
    [InlineData(SpeedScale.Kilo, 166, 1000, "1.33 Kbps")]
    [InlineData(SpeedScale.Kilo, 208, 1000, "1.66 Kbps")]
    [InlineData(SpeedScale.Kilo, 250, 1000, "2 Kbps")]
    [InlineData(SpeedScale.Kilo, 1250, 1000, "10 Kbps")]
    [InlineData(SpeedScale.Kilo, 62500, 1000, "500 Kbps")]
    [InlineData(SpeedScale.Kilo, 124999, 1000, "999.99 Kbps")]
    // Mega scale tests with small fractional values from bit conversion
    [InlineData(SpeedScale.Mega, 125, 1000, "0 Mbps")]
    [InlineData(SpeedScale.Mega, 625, 1000, "0.01 Mbps")]
    [InlineData(SpeedScale.Mega, 1250, 1000, "0.01 Mbps")]
    [InlineData(SpeedScale.Mega, 6250, 1000, "0.05 Mbps")]
    [InlineData(SpeedScale.Mega, 12500, 1000, "0.1 Mbps")]
    [InlineData(SpeedScale.Mega, 31250, 1000, "0.25 Mbps")]
    [InlineData(SpeedScale.Mega, 62500, 1000, "0.5 Mbps")]
    [InlineData(SpeedScale.Mega, 93750, 1000, "0.75 Mbps")]
    [InlineData(SpeedScale.Mega, 125000, 1000, "1 Mbps")]
    [InlineData(SpeedScale.Mega, 156250, 1000, "1.25 Mbps")]
    [InlineData(SpeedScale.Mega, 166666, 1000, "1.33 Mbps")]
    [InlineData(SpeedScale.Mega, 312500, 1000, "2.5 Mbps")]
    // Giga scale tests with very small fractional values from bit conversion
    [InlineData(SpeedScale.Giga, 125000, 1000, "0 Gbps")]
    [InlineData(SpeedScale.Giga, 1250000, 1000, "0.01 Gbps")]
    [InlineData(SpeedScale.Giga, 6250000, 1000, "0.05 Gbps")]
    [InlineData(SpeedScale.Giga, 12500000, 1000, "0.1 Gbps")]
    [InlineData(SpeedScale.Giga, 62500000, 1000, "0.5 Gbps")]
    [InlineData(SpeedScale.Giga, 125000000, 1000, "1 Gbps")]
    [InlineData(SpeedScale.Giga, 156250000, 1000, "1.25 Gbps")]
    // Tera scale tests
    [InlineData(SpeedScale.Tera, 125000000, 1000, "0 Tbps")]
    [InlineData(SpeedScale.Tera, 12500000000, 1000, "0.1 Tbps")]
    [InlineData(SpeedScale.Tera, 62500000000, 1000, "0.5 Tbps")]
    [InlineData(SpeedScale.Tera, 125000000000, 1000, "1 Tbps")]
    // Peta scale tests
    [InlineData(SpeedScale.Peta, 125000000000, 1000, "0 Pbps")]
    [InlineData(SpeedScale.Peta, 12500000000000, 1000, "0.1 Pbps")]
    [InlineData(SpeedScale.Peta, 125000000000000, 1000, "1 Pbps")]
    // Time-based fractional calculations with bit conversion
    [InlineData(SpeedScale.Kilo, 125, 2000, "0.5 Kbps")]
    [InlineData(SpeedScale.Kilo, 125, 4000, "0.25 Kbps")]
    [InlineData(SpeedScale.Kilo, 125, 8000, "0.13 Kbps")]
    [InlineData(SpeedScale.Mega, 125000, 3000, "0.33 Mbps")]
    [InlineData(SpeedScale.Mega, 250000, 3000, "0.67 Mbps")]
    // Edge cases with bit conversion rounding
    [InlineData(SpeedScale.Kilo, 124, 1000, "0.99 Kbps")]
    [InlineData(SpeedScale.Kilo, 999, 1000, "7.99 Kbps")]
    [Theory]
    public void Should_Calculate_Bits_Per_Second_With_Fixed_Scale_SI(SpeedScale scale, long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, scale);
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.SI, scale);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
    }

    /// <summary>
    /// Test fixed scale functionality for bits per second with IEC units, focusing on fractional calculations
    /// using powers of 1024 and the 8x bit multiplier effect.
    /// </summary>
    [InlineData(SpeedScale.Base, 0, 1000, "0 bps")]
    [InlineData(SpeedScale.Base, 1, 1000, "8 bps")]
    [InlineData(SpeedScale.Base, 64, 1000, "512 bps")]
    [InlineData(SpeedScale.Base, 128, 1000, "1024 bps")]
    [InlineData(SpeedScale.Base, 192, 1000, "1536 bps")]
    [InlineData(SpeedScale.Base, 131071, 1000, "1048568 bps")]
    [InlineData(SpeedScale.Base, 131072, 1000, "1048576 bps")]
    // Kibi scale tests with fractional calculations from bit conversion
    [InlineData(SpeedScale.Kilo, 0, 1000, "0 Kibps")]
    [InlineData(SpeedScale.Kilo, 64, 1000, "0.5 Kibps")]
    [InlineData(SpeedScale.Kilo, 128, 1000, "1 Kibps")]
    [InlineData(SpeedScale.Kilo, 160, 1000, "1.25 Kibps")]
    [InlineData(SpeedScale.Kilo, 170, 1000, "1.33 Kibps")]
    [InlineData(SpeedScale.Kilo, 192, 1000, "1.5 Kibps")]
    [InlineData(SpeedScale.Kilo, 213, 1000, "1.66 Kibps")]
    [InlineData(SpeedScale.Kilo, 256, 1000, "2 Kibps")]
    [InlineData(SpeedScale.Kilo, 1280, 1000, "10 Kibps")]
    [InlineData(SpeedScale.Kilo, 64000, 1000, "500 Kibps")]
    [InlineData(SpeedScale.Kilo, 131072, 1000, "1024 Kibps")]
    // Mebi scale tests with small fractional values from bit conversion
    [InlineData(SpeedScale.Mega, 128, 1000, "0 Mibps")]
    [InlineData(SpeedScale.Mega, 640, 1000, "0 Mibps")]
    [InlineData(SpeedScale.Mega, 1310, 1000, "0.01 Mibps")]
    [InlineData(SpeedScale.Mega, 6553, 1000, "0.05 Mibps")]
    [InlineData(SpeedScale.Mega, 13107, 1000, "0.1 Mibps")]
    [InlineData(SpeedScale.Mega, 32768, 1000, "0.25 Mibps")]
    [InlineData(SpeedScale.Mega, 65536, 1000, "0.5 Mibps")]
    [InlineData(SpeedScale.Mega, 98304, 1000, "0.75 Mibps")]
    [InlineData(SpeedScale.Mega, 131072, 1000, "1 Mibps")]
    [InlineData(SpeedScale.Mega, 163840, 1000, "1.25 Mibps")]
    [InlineData(SpeedScale.Mega, 174762, 1000, "1.33 Mibps")]
    [InlineData(SpeedScale.Mega, 196608, 1000, "1.5 Mibps")]
    [InlineData(SpeedScale.Mega, 327680, 1000, "2.5 Mibps")]
    // Gibi scale tests with very small fractional values from bit conversion
    [InlineData(SpeedScale.Giga, 131072, 1000, "0 Gibps")]
    [InlineData(SpeedScale.Giga, 1342177, 1000, "0.01 Gibps")]
    [InlineData(SpeedScale.Giga, 6710886, 1000, "0.05 Gibps")]
    [InlineData(SpeedScale.Giga, 13421772, 1000, "0.1 Gibps")]
    [InlineData(SpeedScale.Giga, 67108864, 1000, "0.5 Gibps")]
    [InlineData(SpeedScale.Giga, 134217728, 1000, "1 Gibps")]
    [InlineData(SpeedScale.Giga, 167772160, 1000, "1.25 Gibps")]
    // Tebi scale tests
    [InlineData(SpeedScale.Tera, 134217728, 1000, "0 Tibps")]
    [InlineData(SpeedScale.Tera, 13743895347, 1000, "0.1 Tibps")]
    [InlineData(SpeedScale.Tera, 68719476736, 1000, "0.5 Tibps")]
    [InlineData(SpeedScale.Tera, 137438953472, 1000, "1 Tibps")]
    // Pebi scale tests
    [InlineData(SpeedScale.Peta, 137438953472, 1000, "0 Pibps")]
    [InlineData(SpeedScale.Peta, 14073748835532, 1000, "0.1 Pibps")]
    [InlineData(SpeedScale.Peta, 140737488355328, 1000, "1 Pibps")]
    // Time-based fractional calculations with binary precision and bit conversion
    [InlineData(SpeedScale.Kilo, 128, 2000, "0.5 Kibps")]
    [InlineData(SpeedScale.Kilo, 128, 4000, "0.25 Kibps")]
    [InlineData(SpeedScale.Kilo, 128, 8000, "0.13 Kibps")]
    [InlineData(SpeedScale.Mega, 131072, 3000, "0.33 Mibps")]
    [InlineData(SpeedScale.Mega, 262144, 3000, "0.67 Mibps")]
    // Edge cases around transition boundaries with bit conversion
    [InlineData(SpeedScale.Kilo, 127, 1000, "0.99 Kibps")]
    [InlineData(SpeedScale.Kilo, 1023, 1000, "7.99 Kibps")]
    [InlineData(SpeedScale.Mega, 131071, 1000, "1 Mibps")]
    [InlineData(SpeedScale.Mega, 131070, 1000, "1 Mibps")]
    [Theory]
    public void Should_Calculate_Bits_Per_Second_With_Fixed_Scale_IEC(SpeedScale scale, long bytesProcessed, long elapsedMilliseconds, string expected)
    {
        // Given
        var result = new SpeedTestResult { BytesProcessed = bytesProcessed, ElapsedMilliseconds = elapsedMilliseconds };

        // When
        var speedString = result.GetSpeedString(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC, scale);
        var (speed, unit) = result.GetSpeedStringParts(SpeedUnit.BitsPerSecond, SpeedUnitSystem.IEC, scale);

        // Then
        Assert.Equal(expected, speedString);
        Assert.Equal(expected, $"{speed} {unit}");
    }
}
