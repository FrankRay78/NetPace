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

    [Fact]
    public void Towards_AResultThatMeasuredLatency_ReportsNeitherFigureUntilAProbeHasAnswered()
    {
        // SCENARIO: Library progress carries running figures

        // Given a result that measured latency under load.
        var result = MeasuredUnderLoad(latencyMilliseconds: 60, jitterMilliseconds: 4);

        // When
        var reports = SpeedTestProgressClimb.Towards(result).ToArray();

        // Then the first report carries neither figure, because no probe had answered by then;
        // the second carries the latency alone, unevenness needing a second probe to compare; and
        // the rest carry both.
        Assert.Null(reports[0].LatencyMilliseconds);
        Assert.Null(reports[0].JitterMilliseconds);
        Assert.Equal(60, reports[1].LatencyMilliseconds);
        Assert.Null(reports[1].JitterMilliseconds);
        Assert.All(reports[2..], report =>
        {
            Assert.Equal(60, report.LatencyMilliseconds);
            Assert.Equal(4, report.JitterMilliseconds);
        });
    }

    [Fact]
    public void Towards_AResultThatMeasuredNoLatency_ReportsNeitherFigureAtAll()
    {
        // Given a result whose transfer measured no latency under load.
        var result = Measured(bytes: 1000, elapsedMilliseconds: 1000);

        // When
        var reports = SpeedTestProgressClimb.Towards(result).ToArray();

        // Then nothing the climb reports claims a figure the result does not have.
        Assert.All(reports, report =>
        {
            Assert.Null(report.LatencyMilliseconds);
            Assert.Null(report.JitterMilliseconds);
        });
    }

    private static SpeedTestResult Measured(long bytes, long elapsedMilliseconds) =>
        new() { BytesProcessed = bytes, ElapsedMilliseconds = elapsedMilliseconds, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null };

    private static SpeedTestResult MeasuredUnderLoad(long latencyMilliseconds, long jitterMilliseconds) =>
        new()
        {
            BytesProcessed = 1000,
            ElapsedMilliseconds = 1000,
            RequestsSucceeded = 1,
            RequestsFailed = 0,
            Latency = new LatencyTestResult
            {
                Server = new Server { Url = "http://test1.com", Sponsor = "Test Sponsor 1" },
                LatencyMilliseconds = latencyMilliseconds,
                MinimumLatencyMilliseconds = latencyMilliseconds - jitterMilliseconds,
                MaximumLatencyMilliseconds = latencyMilliseconds + jitterMilliseconds,
                JitterMilliseconds = jitterMilliseconds
            }
        };
}
