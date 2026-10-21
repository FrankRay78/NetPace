using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

public sealed class SpeedTestProgressClimbTests
{
    [Fact]
    public void Towards_AMeasuredResult_ReportsIncreasingProgress()
    {
        // Given
        var result = Measured(bytes: 1000, elapsedMilliseconds: 1000);

        // When
        var reports = SpeedTestProgressClimb.Towards(result).ToArray();

        // Then the clock and the bytes both only ever advance.
        Assert.Equal([25, 50, 75, 100], reports.Select(report => report.PercentageComplete));
        Assert.Equal(reports.Select(report => report.BytesProcessed).Order(), reports.Select(report => report.BytesProcessed));
        Assert.Equal(reports.Select(report => report.ElapsedMilliseconds).Order(), reports.Select(report => report.ElapsedMilliseconds));
    }

    [Fact]
    public void Towards_AMeasuredResult_ReportsNoBytesWithoutTimeToHaveCarriedThem()
    {
        // Given
        var result = Measured(bytes: 1000, elapsedMilliseconds: 1000);

        // When
        var reports = SpeedTestProgressClimb.Towards(result).ToArray();

        // Then every report can be turned into a speed, so a consumer showing one never divides by
        // a zero clock.
        Assert.All(reports, report =>
        {
            Assert.True(report.BytesProcessed > 0);
            Assert.True(report.ElapsedMilliseconds > 0);
        });
    }

    [Fact]
    public void Towards_TheLastReport_StillTrailsTheResult()
    {
        // Given
        var result = Measured(bytes: 1000, elapsedMilliseconds: 1000);

        // When
        var last = SpeedTestProgressClimb.Towards(result).Last();

        // Then the climb stops short of the result's own speed, so a consumer that never settles the
        // figure on the result shows a different number from the one the result reports. A stub
        // whose last report already matched the result could not tell the two apart.
        Assert.Equal(100, last.PercentageComplete);
        Assert.Equal(result.ElapsedMilliseconds, last.ElapsedMilliseconds);
        Assert.True(last.BytesProcessed < result.BytesProcessed);
    }

    [Fact]
    public void Towards_ANullResult_Throws()
    {
        // When, Then
        Assert.Throws<ArgumentNullException>(() => SpeedTestProgressClimb.Towards(null!).ToArray());
    }

    private static SpeedTestResult Measured(long bytes, long elapsedMilliseconds) =>
        new() { BytesProcessed = bytes, ElapsedMilliseconds = elapsedMilliseconds, RequestsSucceeded = 1, RequestsFailed = 0 };
}
