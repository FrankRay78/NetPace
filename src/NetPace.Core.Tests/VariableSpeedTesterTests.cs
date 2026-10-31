using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

// SCENARIO: The variable tester follows its documented sequence
public sealed class VariableSpeedTesterTests
{
    private const string NamedUrl = "http://named.com";

    [Fact]
    public async Task GetServersAsync_TheVariableTester_ReturnsItsOneDocumentedServer()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var servers = await tester.GetServersAsync();

        // Then a consumer always measures the same server, so only the figures vary.
        var server = Assert.Single(servers);
        Assert.Equal("Test Location", server.Location);
        Assert.Equal("Test Sponsor", server.Sponsor);
        Assert.Equal("http://test.com", server.Url);
    }

    [Fact]
    public async Task ASequenceOfTests_EachRun_ReportsTheDocumentedFiguresForThatRun()
    {
        // Given a consumer running four successive speed tests, each screening a server and then
        // measuring latency, download and upload against it — the shape a repeated run takes.
        var tester = new VariableSpeedTester();

        // When
        var runs = new List<(long Latency, long DownloadBytes, long UploadBytes)>();
        for (int run = 0; run < 4; run++)
        {
            runs.Add(await RunOneTest(tester));
        }

        // Then the figures follow the sequence the tester documents: a slow run, a middling one, a
        // fast one, and the middling one thereafter.
        Assert.Equal([75, 100, 150, 100], runs.Select(result => result.Latency));
        Assert.Equal([31250, 125000, 343750, 125000], runs.Select(result => result.DownloadBytes));
        Assert.Equal([62500, 375000, 166250, 375000], runs.Select(result => result.UploadBytes));
    }

    [Fact]
    public async Task ASequenceOfTests_EachRun_MeasuresOverOneSecond()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var elapsed = new List<long>();
        for (int run = 0; run < 4; run++)
        {
            var screened = await tester.GetFastestServerByLatencyAsync(await tester.GetServersAsync());
            elapsed.Add((await tester.GetDownloadSpeedAsync(screened.Server)).ElapsedMilliseconds);
            elapsed.Add((await tester.GetUploadSpeedAsync(screened.Server)).ElapsedMilliseconds);
        }

        // Then every figure is bytes over the same one second, so a consumer comparing two runs is
        // comparing speeds rather than durations.
        Assert.All(elapsed, milliseconds => Assert.Equal(1000, milliseconds));
    }

    [Fact]
    public async Task ASequenceOfTests_EachRun_ReportsRequestCountsThatRiseWithTheSpeed()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var downloads = new List<int>();
        var uploads = new List<int>();
        for (int run = 0; run < 4; run++)
        {
            var screened = await tester.GetFastestServerByLatencyAsync(await tester.GetServersAsync());
            downloads.Add((await tester.GetDownloadSpeedAsync(screened.Server)).RequestsSucceeded);
            uploads.Add((await tester.GetUploadSpeedAsync(screened.Server)).RequestsSucceeded);
        }

        // Then
        Assert.Equal([40, 150, 400, 150], downloads);
        Assert.Equal([12, 64, 30, 64], uploads);
    }

    [Fact]
    public async Task ASequenceOfTests_EveryRun_SucceedsWithNoFailedRequests()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var failures = new List<int>();
        for (int run = 0; run < 4; run++)
        {
            var screened = await tester.GetFastestServerByLatencyAsync(await tester.GetServersAsync());
            failures.Add((await tester.GetDownloadSpeedAsync(screened.Server)).RequestsFailed);
            failures.Add((await tester.GetUploadSpeedAsync(screened.Server)).RequestsFailed);
        }

        // Then the tester varies the speed, never the success of the run.
        Assert.All(failures, failed => Assert.Equal(0, failed));
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithinOneTest_AgreesWithTheScreeningFigure()
    {
        // Given a caller that screens the servers and then measures the winner, so both calls land
        // within one test.
        var tester = new VariableSpeedTester();
        var servers = await tester.GetServersAsync();

        // When
        var screened = await tester.GetFastestServerByLatencyAsync(servers);
        var measured = await tester.GetServerLatencyAsync(screened.Server);

        // Then both report the figure that test stands for, so a consumer is not shown two
        // different latencies for one run.
        Assert.Equal(screened.LatencyMilliseconds, measured.LatencyMilliseconds);
        Assert.Equal(75, measured.LatencyMilliseconds);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_TheVariableTester_WinsTheFirstCandidateOffered()
    {
        // Given
        var tester = new VariableSpeedTester();
        var candidates = new IServer[]
        {
            new Server { Location = "First", Sponsor = "First Sponsor", Url = "http://first.com" },
            new Server { Location = "Second", Sponsor = "Second Sponsor", Url = "http://second.com" },
        };

        // When
        var result = await tester.GetFastestServerByLatencyAsync(candidates);

        // Then the winner is fixed, so the figures are the only thing that varies.
        Assert.Same(candidates[0], result.Server);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AServerNamedByUrl_ReportsTheFigureForThatTest()
    {
        // Given a consumer that names its own server rather than letting the tester choose.
        var tester = new VariableSpeedTester();

        // When
        var result = await tester.GetServerLatencyAsync(NamedUrl);

        // Then the named server is measured like any other, at the figure the current test stands
        // for, rather than refusing a route the real provider supports.
        Assert.Equal(NamedUrl, result.Server.Url);
        Assert.Equal(100, result.LatencyMilliseconds);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AServerNamedByUrl_ReportsNoLocation()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var result = await tester.GetServerLatencyAsync(NamedUrl);

        // Then the server carries the URL and the sponsor placeholder, and nothing stands in for a
        // location the caller never supplied — the shape the real provider builds.
        Assert.Equal("(Unknown)", result.Server.Sponsor);
        Assert.Empty(result.Server.Location);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AServerNamedByUrlWithinOneTest_AgreesWithTheScreeningFigure()
    {
        // Given
        var tester = new VariableSpeedTester();

        // When
        var screened = await tester.GetFastestServerByLatencyAsync(await tester.GetServersAsync());
        var measured = await tester.GetServerLatencyAsync(NamedUrl);

        // Then naming the server by URL reports the same figure as naming it directly.
        Assert.Equal(screened.LatencyMilliseconds, measured.LatencyMilliseconds);
    }

    [Fact]
    public async Task EveryOverload_OfEveryOperation_ReportsTheFigureForTheSameTest()
    {
        // Given a tester held at its first test, so every overload should report that test's
        // figures whichever way a consumer reaches it.
        var tester = new VariableSpeedTester();
        var servers = await tester.GetServersAsync();
        await tester.GetFastestServerByLatencyAsync(servers);
        var server = servers.Single();
        var latencyProgress = new NullProgress<LatencyTestProgress>();
        var speedProgress = new NullProgress<SpeedTestProgress>();

        // When
        var latencies = new List<long>
        {
            (await tester.GetServerLatencyAsync(server)).LatencyMilliseconds,
            (await tester.GetServerLatencyAsync(server, latencyProgress)).LatencyMilliseconds,
            (await tester.GetServerLatencyAsync(server, latencyProgress, null)).LatencyMilliseconds,
            (await tester.GetServerLatencyAsync(NamedUrl)).LatencyMilliseconds,
            (await tester.GetServerLatencyAsync(NamedUrl, latencyProgress)).LatencyMilliseconds,
            (await tester.GetServerLatencyAsync(NamedUrl, latencyProgress, null)).LatencyMilliseconds,
            (await tester.GetFastestServerByLatencyAsync(servers, speedProgress)).LatencyMilliseconds,
            (await tester.GetFastestServerByLatencyAsync(servers, speedProgress, null)).LatencyMilliseconds,
        };
        var downloads = new List<long>
        {
            (await tester.GetDownloadSpeedAsync(server)).BytesProcessed,
            (await tester.GetDownloadSpeedAsync(server, speedProgress)).BytesProcessed,
            (await tester.GetDownloadSpeedAsync(server, speedProgress, null)).BytesProcessed,
        };
        var uploads = new List<long>
        {
            (await tester.GetUploadSpeedAsync(server)).BytesProcessed,
            (await tester.GetUploadSpeedAsync(server, speedProgress)).BytesProcessed,
            (await tester.GetUploadSpeedAsync(server, speedProgress, null)).BytesProcessed,
        };

        // Then only screening advances the sequence, and the two screening calls above advance it
        // to the third test; every other overload reports the test it was called within.
        Assert.Equal([75, 75, 75, 75, 75, 75, 100, 150], latencies);
        Assert.Equal([343750, 343750, 343750], downloads);
        Assert.Equal([166250, 166250, 166250], uploads);
    }

    /// <summary>
    /// One speed test as a consumer runs it: screen the servers, then measure latency, download and
    /// upload against the winner.
    /// </summary>
    private static async Task<(long Latency, long DownloadBytes, long UploadBytes)> RunOneTest(ISpeedTestService tester)
    {
        var servers = await tester.GetServersAsync();
        var screened = await tester.GetFastestServerByLatencyAsync(servers);
        var latency = await tester.GetServerLatencyAsync(screened.Server);
        var download = await tester.GetDownloadSpeedAsync(screened.Server);
        var upload = await tester.GetUploadSpeedAsync(screened.Server);

        return (latency.LatencyMilliseconds, download.BytesProcessed, upload.BytesProcessed);
    }
}
