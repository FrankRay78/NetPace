using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

// SCENARIO: The faulty tester fails as documented
public sealed class FaultySpeedTesterTests
{
    private const string FaultySponsor = "Test Sponsor 2";

    private const string FaultyUrl = "http://test2.com";

    private const string NamedUrl = "http://named.com";

    /// <summary>
    /// Each way a caller can ask the faulty tester for the latency of a server it was handed — the
    /// one operation its default behaviour fails.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer, Task<LatencyTestResult>>> LatencyByServerOverloads => new()
    {
        { "server", (tester, server) => tester.GetServerLatencyAsync(server) },
        { "server, progress", (tester, server) => tester.GetServerLatencyAsync(server, new NullProgress<LatencyTestProgress>()) },
        { "server, progress, diagnostics", (tester, server) => tester.GetServerLatencyAsync(server, null, null) },
    };

    /// <summary>
    /// Each way a caller can measure a transfer — operations the default behaviour leaves alone —
    /// paired with the figures the wrapped stub reports for the unreachable server.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer, Task<SpeedTestResult>>, long, long> TransferOverloads => new()
    {
        { "download: server", (tester, server) => tester.GetDownloadSpeedAsync(server), 1000, 2000 },
        { "download: server, progress", (tester, server) => tester.GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>()), 1000, 2000 },
        { "download: server, progress, diagnostics", (tester, server) => tester.GetDownloadSpeedAsync(server, null, null), 1000, 2000 },
        { "upload: server", (tester, server) => tester.GetUploadSpeedAsync(server), 7000, 6000 },
        { "upload: server, progress", (tester, server) => tester.GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>()), 7000, 6000 },
        { "upload: server, progress, diagnostics", (tester, server) => tester.GetUploadSpeedAsync(server, null, null), 7000, 6000 },
    };

    /// <summary>
    /// Each way a caller can screen a list of candidates.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer[], Task<LatencyTestResult>>> ScreeningOverloads => new()
    {
        { "servers", (tester, servers) => tester.GetFastestServerByLatencyAsync(servers) },
        { "servers, progress", (tester, servers) => tester.GetFastestServerByLatencyAsync(servers, new NullProgress<SpeedTestProgress>()) },
        { "servers, progress, diagnostics", (tester, servers) => tester.GetFastestServerByLatencyAsync(servers, null, null) },
    };

    /// <summary>
    /// Each way a caller can ask for the latency of a server named by URL.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, string, Task<LatencyTestResult>>> LatencyByUrlOverloads => new()
    {
        { "url", (tester, url) => tester.GetServerLatencyAsync(url) },
        { "url, progress", (tester, url) => tester.GetServerLatencyAsync(url, new NullProgress<LatencyTestProgress>()) },
        { "url, progress, diagnostics", (tester, url) => tester.GetServerLatencyAsync(url, null, null) },
    };

    [Fact]
    public async Task GetServersAsync_TheFaultyTester_ReturnsTheServersItWrapsIncludingTheUnreachableOne()
    {
        // Given
        var tester = new FaultySpeedTester();

        // When
        var servers = await tester.GetServersAsync();

        // Then listing servers never fails, so a consumer discovers the unreachable one and only
        // finds out it is unreachable when it tries to measure it.
        Assert.Equal(3, servers.Length);
        Assert.Contains(servers, server => server.Sponsor == FaultySponsor);
    }

    [Theory]
    [MemberData(nameof(LatencyByServerOverloads))]
    public async Task GetServerLatencyAsync_TheUnreachableServer_Fails(string overload, Func<ISpeedTestService, IServer, Task<LatencyTestResult>> measure)
    {
        // Given the default behaviour, which fails a latency probe to one of the three servers.
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var unreachable = (await tester.GetServersAsync()).Single(server => server.Sponsor == FaultySponsor);

        // When, Then a consumer testing its own error handling sees the probe fail.
        await Assert.ThrowsAsync<Exception>(() => measure(tester, unreachable));
    }

    [Theory]
    [MemberData(nameof(LatencyByServerOverloads))]
    public async Task GetServerLatencyAsync_AReachableServer_Succeeds(string overload, Func<ISpeedTestService, IServer, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var reachable = (await tester.GetServersAsync()).First();

        // When
        var result = await measure(tester, reachable);

        // Then the tester fails selectively, so a consumer can tell a handled failure from a
        // broken double.
        Assert.Equal(100, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(TransferOverloads))]
    public async Task ATransfer_ToTheUnreachableServer_Succeeds(string overload, Func<ISpeedTestService, IServer, Task<SpeedTestResult>> measure, long expectedBytes, long expectedElapsed)
    {
        // Given the default behaviour, which fails only the latency probe.
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var unreachable = (await tester.GetServersAsync()).Single(server => server.Sponsor == FaultySponsor);

        // When
        var result = await measure(tester, unreachable);

        // Then a consumer that reaches a transfer at all gets the wrapped service's own figures,
        // because the default fault is scoped to one operation rather than to the server as a whole.
        Assert.Equal(expectedBytes, result.BytesProcessed);
        Assert.Equal(expectedElapsed, result.ElapsedMilliseconds);
    }

    [Theory]
    [MemberData(nameof(ScreeningOverloads))]
    public async Task GetFastestServerByLatencyAsync_WithAnUnreachableCandidate_WinsFromTheRest(string overload, Func<ISpeedTestService, IServer[], Task<LatencyTestResult>> screen)
    {
        // Given
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var servers = await tester.GetServersAsync();

        // When
        var result = await screen(tester, servers);

        // Then a candidate the tester cannot probe drops out of selection rather than being
        // ranked, so the lowest-numbered reachable server wins.
        Assert.Equal("Test Sponsor 1", result.Server.Sponsor);
    }

    [Theory]
    [MemberData(nameof(ScreeningOverloads))]
    public async Task GetFastestServerByLatencyAsync_WithNothingReachable_Fails(string overload, Func<ISpeedTestService, IServer[], Task<LatencyTestResult>> screen)
    {
        // Given a selection offered only the server the tester cannot probe.
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var unreachable = (await tester.GetServersAsync()).Single(server => server.Sponsor == FaultySponsor);

        // When, Then a selection left with nothing reports that no servers are available rather
        // than returning the candidate it could not measure.
        await Assert.ThrowsAsync<Exception>(() => screen(tester, [unreachable]));
    }

    [Theory]
    [MemberData(nameof(ScreeningOverloads))]
    public async Task GetFastestServerByLatencyAsync_WithOnlyReachableCandidates_WinsTheFastest(string overload, Func<ISpeedTestService, IServer[], Task<LatencyTestResult>> screen)
    {
        // Given
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();
        var reachable = (await tester.GetServersAsync()).Where(server => server.Sponsor != FaultySponsor).ToArray();

        // When
        var result = await screen(tester, reachable);

        // Then the tester ranks what it can probe exactly as the service it wraps would.
        Assert.Equal(100, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(LatencyByUrlOverloads))]
    public async Task GetServerLatencyAsync_AServerNamedByUrl_IsJudgedOnTheServerTheProbeReports(string overload, Func<ISpeedTestService, string, Task<LatencyTestResult>> measure)
    {
        // Given the service the tester wraps names a URL-specified server with a placeholder
        // sponsor, which the default fault does not match.
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester();

        // When
        var result = await measure(tester, FaultyUrl);

        // Then a consumer naming the unreachable server's URL is not failed on its sponsor, which
        // the probe only learns after the fact.
        Assert.Equal(200, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(LatencyByUrlOverloads))]
    public async Task GetServerLatencyAsync_AServerNamedByUrlThatProbesAsUnreachable_Fails(string overload, Func<ISpeedTestService, string, Task<LatencyTestResult>> measure)
    {
        // Given a tester wrapping a service that reports the unreachable sponsor for any URL.
        Assert.NotNull(overload);
        var tester = new FaultySpeedTester(inner: new SpeedTestMock
        {
            GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => Task.FromResult(new LatencyTestResult
            {
                Server = new Server { Sponsor = FaultySponsor, Url = url },
                LatencyMilliseconds = 1,
                JitterMilliseconds = 0,
            }),
        });

        // When, Then the fault is applied to the server the probe came back with, so naming an
        // unreachable server by URL fails too.
        await Assert.ThrowsAsync<Exception>(() => measure(tester, NamedUrl));
    }

    [Fact]
    public async Task AFaultyTester_BuiltWithAnInnerService_DelegatesEveryOperationToIt()
    {
        // Given a consumer that wants its own figures behind the failures.
        var server = new Server { Location = "Anywhere", Sponsor = "Anyone", Url = "http://anywhere.com" };
        var tester = new FaultySpeedTester(inner: new SpeedTestMock
        {
            GetServersAsyncFunc = _ => Task.FromResult<IServer[]>([server]),
            GetServerLatencyAsyncFunc = (_, _, _) => Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 7, JitterMilliseconds = 1 }),
            GetFastestServerByLatencyAsyncFunc = (servers, _, _) => Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 7, JitterMilliseconds = 0 }),
            GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(new SpeedTestResult { BytesProcessed = 11, ElapsedMilliseconds = 1000, RequestsSucceeded = 1, RequestsFailed = 0 }),
            GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(new SpeedTestResult { BytesProcessed = 13, ElapsedMilliseconds = 1000, RequestsSucceeded = 1, RequestsFailed = 0 }),
        });

        // When
        var servers = await tester.GetServersAsync();
        var screened = await tester.GetFastestServerByLatencyAsync(servers);
        var latency = await tester.GetServerLatencyAsync(server);
        var download = await tester.GetDownloadSpeedAsync(server);
        var upload = await tester.GetUploadSpeedAsync(server);

        // Then every figure is the wrapped service's own, so the tester adds failures without
        // changing what succeeds.
        Assert.Same(server, Assert.Single(servers));
        Assert.Equal(7, screened.LatencyMilliseconds);
        Assert.Equal(7, latency.LatencyMilliseconds);
        Assert.Equal(11, download.BytesProcessed);
        Assert.Equal(13, upload.BytesProcessed);
    }

    [Fact]
    public async Task AFaultyTester_BuiltWithItsOwnFaultRule_FailsWhereThatRuleSays()
    {
        // Given a consumer that chooses which server and which operation fail.
        var tester = new FaultySpeedTester(isFaulted: (sponsor, operation) =>
            sponsor == "Test Sponsor 1" && operation == nameof(ISpeedTestService.GetDownloadSpeedAsync));
        var servers = await tester.GetServersAsync();
        var chosen = servers.Single(server => server.Sponsor == "Test Sponsor 1");

        // When, Then the rule replaces the default entirely: the download it names fails, and the
        // latency probe the default would have failed now succeeds.
        await Assert.ThrowsAsync<Exception>(() => tester.GetDownloadSpeedAsync(chosen));
        var defaultFault = await tester.GetServerLatencyAsync(servers.Single(server => server.Sponsor == FaultySponsor));
        Assert.Equal(200, defaultFault.LatencyMilliseconds);
    }

    [Fact]
    public async Task AFaultyTester_BuiltWithAFaultRuleOnLatency_DropsThatServerFromSelection()
    {
        // Given a consumer that makes a different server unreachable.
        var tester = new FaultySpeedTester(isFaulted: (sponsor, operation) =>
            sponsor == "Test Sponsor 1" && operation == nameof(ISpeedTestService.GetServerLatencyAsync));
        var servers = await tester.GetServersAsync();

        // When
        var result = await tester.GetFastestServerByLatencyAsync(servers);

        // Then screening is itself a latency probe, so the rule governs selection as well as
        // measurement, and the next-lowest-numbered server wins.
        Assert.Equal(FaultySponsor, result.Server.Sponsor);
    }
}
