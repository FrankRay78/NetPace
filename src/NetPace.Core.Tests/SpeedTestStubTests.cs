using System.Diagnostics;
using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

// SCENARIO: The stub reports its documented values
// SCENARIO: The stub's URL-named server matches the real provider's shape
public sealed class SpeedTestStubTests
{
    private const int DelayMilliseconds = 20;

    // Task.Delay fires on the runtime's coarse timer, so one pause can end a fraction of a millisecond
    // short by the Stopwatch's clock, and ElapsedMilliseconds truncates it to a whole millisecond less.
    // A millisecond per pause absorbs that and still fails a stub that pauses once instead of before
    // every report.
    private const int TimerSlackMilliseconds = 1;

    /// <summary>
    /// Each way a caller can ask the stub for the latency of a server it already holds.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer, IProgress<LatencyTestProgress>, Task<LatencyTestResult>>> LatencyByServerOverloads => new()
    {
        { "server", (stub, server, _) => stub.GetServerLatencyAsync(server) },
        { "server, progress", (stub, server, progress) => stub.GetServerLatencyAsync(server, progress) },
        { "server, progress, diagnostics", (stub, server, progress) => stub.GetServerLatencyAsync(server, progress, null) },
    };

    /// <summary>
    /// Each way a caller can ask the stub for the latency of a server named by URL.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, string, IProgress<LatencyTestProgress>, Task<LatencyTestResult>>> LatencyByUrlOverloads => new()
    {
        { "url", (stub, url, _) => stub.GetServerLatencyAsync(url) },
        { "url, progress", (stub, url, progress) => stub.GetServerLatencyAsync(url, progress) },
        { "url, progress, diagnostics", (stub, url, progress) => stub.GetServerLatencyAsync(url, progress, null) },
    };

    /// <summary>
    /// Each way a caller can ask the stub to screen a list of candidates.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer[], IProgress<SpeedTestProgress>, Task<LatencyTestResult>>> ScreeningOverloads => new()
    {
        { "servers", (stub, servers, _) => stub.GetFastestServerByLatencyAsync(servers) },
        { "servers, progress", (stub, servers, progress) => stub.GetFastestServerByLatencyAsync(servers, progress) },
        { "servers, progress, diagnostics", (stub, servers, progress) => stub.GetFastestServerByLatencyAsync(servers, progress, null) },
    };

    /// <summary>
    /// Each way a caller can ask the stub to measure a download, then an upload.
    /// </summary>
    public static TheoryData<string, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>>> DownloadOverloads => new()
    {
        { "server", (stub, server, _) => stub.GetDownloadSpeedAsync(server) },
        { "server, progress", (stub, server, progress) => stub.GetDownloadSpeedAsync(server, progress) },
        { "server, progress, diagnostics", (stub, server, progress) => stub.GetDownloadSpeedAsync(server, progress, null) },
    };

    public static TheoryData<string, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>>> UploadOverloads => new()
    {
        { "server", (stub, server, _) => stub.GetUploadSpeedAsync(server) },
        { "server, progress", (stub, server, progress) => stub.GetUploadSpeedAsync(server, progress) },
        { "server, progress, diagnostics", (stub, server, progress) => stub.GetUploadSpeedAsync(server, progress, null) },
    };

    [Fact]
    public async Task GetServersAsync_TheStub_ReturnsItsThreeDocumentedServers()
    {
        // Given
        var stub = new SpeedTestStub();

        // When
        var servers = await stub.GetServersAsync();

        // Then
        Assert.Equal(3, servers.Length);
        Assert.Equal(["Location 1", "Location 2", "Location 3"], servers.Select(server => server.Location));
        Assert.Equal(["Test Sponsor 1", "Test Sponsor 2", "Test Sponsor 3"], servers.Select(server => server.Sponsor));
        Assert.Equal(["http://test1.com", "http://test2.com", "http://test3.com"], servers.Select(server => server.Url));
    }

    [Fact]
    public async Task GetServersAsync_CalledTwice_ReturnsTheSameServers()
    {
        // Given
        var stub = new SpeedTestStub();

        // When
        var first = await stub.GetServersAsync();
        var second = await stub.GetServersAsync();

        // Then a consumer can measure the same server twice and compare the two runs.
        Assert.Equal(first.Select(server => server.Url), second.Select(server => server.Url));
    }

    [Theory]
    [MemberData(nameof(LatencyByServerOverloads))]
    public async Task GetServerLatencyAsync_EachOverload_ReportsOneHundredMillisecondsPerServerNumber(string overload, Func<ISpeedTestService, IServer, IProgress<LatencyTestProgress>, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var results = new List<LatencyTestResult>();
        foreach (IServer server in servers)
        {
            results.Add(await measure(stub, server, new NullProgress<LatencyTestProgress>()));
        }

        // Then
        Assert.Equal([100, 200, 300], results.Select(result => result.LatencyMilliseconds));
        Assert.Equal(servers.Select(server => server.Url), results.Select(result => result.Server.Url));
    }

    [Theory]
    [MemberData(nameof(LatencyByServerOverloads))]
    public async Task GetServerLatencyAsync_AServerTheStubDoesNotHold_ReportsOneThousandMilliseconds(string overload, Func<ISpeedTestService, IServer, IProgress<LatencyTestProgress>, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var unknown = new Server { Location = "Elsewhere", Sponsor = "Someone Else", Url = "http://elsewhere.com" };

        // When
        var result = await measure(stub, unknown, new NullProgress<LatencyTestProgress>());

        // Then
        Assert.Equal(1000, result.LatencyMilliseconds);
        Assert.Same(unknown, result.Server);
    }

    [Theory]
    [MemberData(nameof(LatencyByUrlOverloads))]
    public async Task GetServerLatencyAsync_AUrlTheStubHolds_ReportsThatServersLatency(string overload, Func<ISpeedTestService, string, IProgress<LatencyTestProgress>, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();

        // When
        var result = await measure(stub, "http://test2.com", new NullProgress<LatencyTestProgress>());

        // Then the URL is matched against the stub's own list, so the figure agrees with the one
        // the same server reports when passed directly.
        Assert.Equal(200, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(LatencyByUrlOverloads))]
    public async Task GetServerLatencyAsync_AUrlTheStubDoesNotHold_ReportsOneThousandMilliseconds(string overload, Func<ISpeedTestService, string, IProgress<LatencyTestProgress>, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();

        // When
        var result = await measure(stub, "http://elsewhere.com", new NullProgress<LatencyTestProgress>());

        // Then
        Assert.Equal(1000, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(LatencyByUrlOverloads))]
    public async Task GetServerLatencyAsync_AServerNamedByUrl_ReportsNoLocation(string overload, Func<ISpeedTestService, string, IProgress<LatencyTestProgress>, Task<LatencyTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();

        // When
        var result = await measure(stub, "http://elsewhere.com", new NullProgress<LatencyTestProgress>());

        // Then the server carries the URL asked for and the sponsor placeholder, and nothing
        // stands in for a location the caller never supplied — the shape the real provider builds.
        Assert.Equal("http://elsewhere.com", result.Server.Url);
        Assert.Equal("(Unknown)", result.Server.Sponsor);
        Assert.Empty(result.Server.Location);
    }

    [Theory]
    [MemberData(nameof(ScreeningOverloads))]
    public async Task GetFastestServerByLatencyAsync_EachOverload_WinsTheLowestNumberedCandidateOffered(string overload, Func<ISpeedTestService, IServer[], IProgress<SpeedTestProgress>, Task<LatencyTestResult>> screen)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var result = await screen(stub, [servers[2], servers[1]], new NullProgress<SpeedTestProgress>());

        // Then the winner comes from the candidates offered rather than the stub's own list, and
        // carries the latency that server reports.
        Assert.Equal("http://test2.com", result.Server.Url);
        Assert.Equal(200, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(ScreeningOverloads))]
    public async Task GetFastestServerByLatencyAsync_ASingleCandidate_WinsUnopposed(string overload, Func<ISpeedTestService, IServer[], IProgress<SpeedTestProgress>, Task<LatencyTestResult>> screen)
    {
        // Given a caller that screens one candidate, as a caller naming its own server does.
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var result = await screen(stub, [servers[2]], new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Equal("http://test3.com", result.Server.Url);
        Assert.Equal(300, result.LatencyMilliseconds);
    }

    [Theory]
    [MemberData(nameof(DownloadOverloads))]
    public async Task GetDownloadSpeedAsync_EachOverload_ReportsOneThousandBytesOverOneSecondPerServerNumber(string overload, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var result = await measure(stub, servers[1], new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Equal(1000, result.BytesProcessed);
        Assert.Equal(2000, result.ElapsedMilliseconds);
        Assert.Equal(150, result.RequestsSucceeded);
        Assert.Equal(0, result.RequestsFailed);
    }

    [Theory]
    [MemberData(nameof(UploadOverloads))]
    public async Task GetUploadSpeedAsync_EachOverload_ReportsSevenThousandBytesOverThreeSecondsPerServerNumber(string overload, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var result = await measure(stub, servers[1], new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Equal(7000, result.BytesProcessed);
        Assert.Equal(6000, result.ElapsedMilliseconds);
        Assert.Equal(32, result.RequestsSucceeded);
        Assert.Equal(0, result.RequestsFailed);
    }

    [Theory]
    [MemberData(nameof(DownloadOverloads))]
    public async Task GetDownloadSpeedAsync_AServerTheStubDoesNotHold_ReportsTenSeconds(string overload, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var unknown = new Server { Sponsor = "Someone Else", Url = "http://elsewhere.com" };

        // When
        var result = await measure(stub, unknown, new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Equal(1000, result.BytesProcessed);
        Assert.Equal(10000, result.ElapsedMilliseconds);
    }

    [Theory]
    [MemberData(nameof(UploadOverloads))]
    public async Task GetUploadSpeedAsync_AServerTheStubDoesNotHold_ReportsThirtySeconds(string overload, Func<ISpeedTestService, IServer, IProgress<SpeedTestProgress>, Task<SpeedTestResult>> measure)
    {
        // Given
        Assert.NotNull(overload);
        var stub = new SpeedTestStub();
        var unknown = new Server { Sponsor = "Someone Else", Url = "http://elsewhere.com" };

        // When
        var result = await measure(stub, unknown, new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Equal(7000, result.BytesProcessed);
        Assert.Equal(30000, result.ElapsedMilliseconds);
    }

    [Fact]
    public async Task GetServerLatencyAsync_WithAProgressReceiver_ClimbsToCompletion()
    {
        // Given
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var reported = new List<int>();

        // When
        await stub.GetServerLatencyAsync(servers[0], new SynchronousProgress<LatencyTestProgress>(progress => reported.Add(progress.PercentageComplete)));

        // Then a consumer watching the probe sees it finish.
        Assert.Equal([25, 50, 75, 100], reported);
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_WithAProgressReceiver_ClimbsToCompletion()
    {
        // Given
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var reported = new List<int>();

        // When
        await stub.GetFastestServerByLatencyAsync(servers, new SynchronousProgress<SpeedTestProgress>(progress => reported.Add(progress.PercentageComplete)));

        // Then
        Assert.Equal([33, 66, 100], reported);
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_WithAProgressReceiver_ReportsTheSharedClimbTowardsItsResult()
    {
        // Given
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var reported = new List<SpeedTestProgress>();

        // When
        var result = await stub.GetDownloadSpeedAsync(servers[0], new SynchronousProgress<SpeedTestProgress>(reported.Add));

        // Then the reports are the climb every stubbed measurement shares, towards this result.
        Assert.Equal(SpeedTestProgressClimb.Towards(result), reported);
    }

    [Fact]
    public async Task GetUploadSpeedAsync_WithAProgressReceiver_ReportsTheSharedClimbTowardsItsResult()
    {
        // Given
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var reported = new List<SpeedTestProgress>();

        // When
        var result = await stub.GetUploadSpeedAsync(servers[0], new SynchronousProgress<SpeedTestProgress>(reported.Add));

        // Then
        Assert.Equal(SpeedTestProgressClimb.Towards(result), reported);
    }

    [Fact]
    public async Task GetServerLatencyAsync_TheDiagnosticsOverload_ReportsNoRequests()
    {
        // Given the stub fabricates aggregate results rather than issuing requests.
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var diagnostics = new List<RequestDiagnostic>();

        // When
        await stub.GetServerLatencyAsync(servers[0], null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));
        await stub.GetServerLatencyAsync("http://test1.com", null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));
        await stub.GetFastestServerByLatencyAsync(servers, null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));
        await stub.GetDownloadSpeedAsync(servers[0], null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));
        await stub.GetUploadSpeedAsync(servers[0], null, new SynchronousProgress<RequestDiagnostic>(diagnostics.Add));

        // Then
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task GetServerLatencyAsync_TheDiagnosticsOverloadWithoutAProgressReceiver_StillReportsAResult()
    {
        // Given a caller that wants the result and watches neither progress nor requests.
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var latency = await stub.GetServerLatencyAsync(servers[0], null, null);
        var byUrl = await stub.GetServerLatencyAsync("http://test1.com", null, null);
        var screened = await stub.GetFastestServerByLatencyAsync(servers, null, null);
        var download = await stub.GetDownloadSpeedAsync(servers[0], null, null);
        var upload = await stub.GetUploadSpeedAsync(servers[0], null, null);

        // Then
        Assert.Equal(100, latency.LatencyMilliseconds);
        Assert.Equal(100, byUrl.LatencyMilliseconds);
        Assert.Equal(100, screened.LatencyMilliseconds);
        Assert.Equal(1000, download.BytesProcessed);
        Assert.Equal(7000, upload.BytesProcessed);
    }

    [Fact]
    public async Task GetServerLatencyAsync_AStubBuiltWithADelay_TakesAtLeastThatDelayBeforeEachReport()
    {
        // Given a stub that pauses before each of its four latency reports.
        var stub = new SpeedTestStub(DelayMilliseconds);
        var servers = await stub.GetServersAsync();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.GetServerLatencyAsync(servers[0], new NullProgress<LatencyTestProgress>());
        stopwatch.Stop();

        // Then a consumer can watch a stubbed probe unfold rather than complete instantly.
        const int Reports = 4;
        Assert.True(stopwatch.ElapsedMilliseconds >= Reports * (DelayMilliseconds - TimerSlackMilliseconds), $"expected at least {Reports * (DelayMilliseconds - TimerSlackMilliseconds)}ms across {Reports} reports, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetDownloadSpeedAsync_AStubBuiltWithADelay_TakesAtLeastThatDelayBeforeEachReport()
    {
        // Given
        var stub = new SpeedTestStub(DelayMilliseconds);
        var servers = await stub.GetServersAsync();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.GetDownloadSpeedAsync(servers[0], new NullProgress<SpeedTestProgress>());
        stopwatch.Stop();

        // Then
        const int Reports = 4;
        Assert.True(stopwatch.ElapsedMilliseconds >= Reports * (DelayMilliseconds - TimerSlackMilliseconds), $"expected at least {Reports * (DelayMilliseconds - TimerSlackMilliseconds)}ms across {Reports} reports, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetUploadSpeedAsync_AStubBuiltWithADelay_TakesAtLeastThatDelayBeforeEachReport()
    {
        // Given
        var stub = new SpeedTestStub(DelayMilliseconds);
        var servers = await stub.GetServersAsync();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.GetUploadSpeedAsync(servers[0], new NullProgress<SpeedTestProgress>());
        stopwatch.Stop();

        // Then
        const int Reports = 4;
        Assert.True(stopwatch.ElapsedMilliseconds >= Reports * (DelayMilliseconds - TimerSlackMilliseconds), $"expected at least {Reports * (DelayMilliseconds - TimerSlackMilliseconds)}ms across {Reports} reports, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetFastestServerByLatencyAsync_AStubBuiltWithADelay_TakesAtLeastThatDelayBeforeEachReport()
    {
        // Given
        var stub = new SpeedTestStub(DelayMilliseconds);
        var servers = await stub.GetServersAsync();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.GetFastestServerByLatencyAsync(servers, new NullProgress<SpeedTestProgress>());
        stopwatch.Stop();

        // Then
        const int Reports = 3;
        Assert.True(stopwatch.ElapsedMilliseconds >= Reports * (DelayMilliseconds - TimerSlackMilliseconds), $"expected at least {Reports * (DelayMilliseconds - TimerSlackMilliseconds)}ms across {Reports} reports, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task GetServerLatencyAsync_AStubBuiltWithoutADelay_CompletesWithoutPausing()
    {
        // Given the parameterless stub, which a consumer uses when it wants results at once.
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var stopwatch = Stopwatch.StartNew();
        await stub.GetServerLatencyAsync(servers[0]);
        stopwatch.Stop();

        // Then the probe does not pause, so a consumer's suite is not slowed by a stub it asked
        // for no delay from.
        Assert.True(stopwatch.ElapsedMilliseconds < DelayMilliseconds, $"expected no pause, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task TheDiagnosticsOverloads_WithAProgressReceiver_StillReportProgressToIt()
    {
        // Given a stub reached through the three-argument overloads, which a consumer uses when it
        // wants diagnostics alongside progress.
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();
        var latencyReports = new List<int>();
        var speedReports = new List<int>();
        var latencyProgress = new SynchronousProgress<LatencyTestProgress>(progress => latencyReports.Add(progress.PercentageComplete));
        var speedProgress = new SynchronousProgress<SpeedTestProgress>(progress => speedReports.Add(progress.PercentageComplete));

        // When
        await stub.GetServerLatencyAsync(servers[0], latencyProgress, null);
        await stub.GetServerLatencyAsync(servers[0].Url, latencyProgress, null);
        await stub.GetFastestServerByLatencyAsync(servers, speedProgress, null);
        await stub.GetDownloadSpeedAsync(servers[0], speedProgress, null);
        await stub.GetUploadSpeedAsync(servers[0], speedProgress, null);

        // Then asking for diagnostics does not cost a consumer the progress it also asked for.
        Assert.Equal([25, 50, 75, 100, 25, 50, 75, 100], latencyReports);
        Assert.Equal([33, 66, 100, 25, 50, 75, 100, 25, 50, 75, 100], speedReports);
    }

    [Fact]
    public async Task TheProgressOverloads_WithANullProgressReceiver_ReportTheirResultsAsTheRealProviderDoes()
    {
        // Given a consumer that passes null where a progress receiver goes, which the real
        // provider treats as no receiver at all.
        var stub = new SpeedTestStub();
        var servers = await stub.GetServersAsync();

        // When
        var latency = await stub.GetServerLatencyAsync(servers[0], (IProgress<LatencyTestProgress>?)null);
        var byUrl = await stub.GetServerLatencyAsync("http://test1.com", (IProgress<LatencyTestProgress>?)null);
        var screened = await stub.GetFastestServerByLatencyAsync(servers, (IProgress<SpeedTestProgress>?)null);
        var download = await stub.GetDownloadSpeedAsync(servers[0], (IProgress<SpeedTestProgress>?)null);
        var upload = await stub.GetUploadSpeedAsync(servers[0], (IProgress<SpeedTestProgress>?)null);

        // Then the stub stands in for the provider here too, rather than failing where it would not.
        Assert.Equal(100, latency.LatencyMilliseconds);
        Assert.Equal(100, byUrl.LatencyMilliseconds);
        Assert.Equal(100, screened.LatencyMilliseconds);
        Assert.Equal(1000, download.BytesProcessed);
        Assert.Equal(7000, upload.BytesProcessed);
    }
}
