using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

// SCENARIO: The mock calls what it is given
public sealed class SpeedTestMockTests
{
    private static readonly IServer Anywhere = new Server { Location = "Anywhere", Sponsor = "Anyone", Url = "http://anywhere.com" };

    private const string NamedUrl = "http://named.com";

    /// <summary>
    /// Every operation of the mock: its name, a setter that injects a function returning a result
    /// this test holds on to, and the call a consumer makes to reach it.
    /// </summary>
    /// <remarks>
    /// The injected function's result is returned to the test as well as configured on the mock,
    /// so a single theory can assert the mock handed back that very object whichever result type
    /// the operation returns.
    /// </remarks>
    public static TheoryData<string, Func<SpeedTestMock, object>, Func<SpeedTestMock, IProgress<LatencyTestProgress>, IProgress<SpeedTestProgress>, Task<object>>> Operations => new()
    {
        {
            "GetServersAsync",
            InjectServers,
            async (mock, _, _) => await mock.GetServersAsync()
        },
        {
            "GetServerLatencyAsync(server)",
            InjectLatencyByServer,
            async (mock, _, _) => await mock.GetServerLatencyAsync(Anywhere)
        },
        {
            "GetServerLatencyAsync(server, progress)",
            InjectLatencyByServer,
            async (mock, latencyProgress, _) => await mock.GetServerLatencyAsync(Anywhere, latencyProgress)
        },
        {
            "GetServerLatencyAsync(server, progress, diagnostics)",
            InjectLatencyByServer,
            async (mock, latencyProgress, _) => await mock.GetServerLatencyAsync(Anywhere, latencyProgress, null)
        },
        {
            "GetServerLatencyAsync(url)",
            InjectLatencyByUrl,
            async (mock, _, _) => await mock.GetServerLatencyAsync(NamedUrl)
        },
        {
            "GetServerLatencyAsync(url, progress)",
            InjectLatencyByUrl,
            async (mock, latencyProgress, _) => await mock.GetServerLatencyAsync(NamedUrl, latencyProgress)
        },
        {
            "GetServerLatencyAsync(url, progress, diagnostics)",
            InjectLatencyByUrl,
            async (mock, latencyProgress, _) => await mock.GetServerLatencyAsync(NamedUrl, latencyProgress, null)
        },
        {
            "GetFastestServerByLatencyAsync(servers)",
            InjectScreening,
            async (mock, _, _) => await mock.GetFastestServerByLatencyAsync([Anywhere])
        },
        {
            "GetFastestServerByLatencyAsync(servers, progress)",
            InjectScreening,
            async (mock, _, speedProgress) => await mock.GetFastestServerByLatencyAsync([Anywhere], speedProgress)
        },
        {
            "GetFastestServerByLatencyAsync(servers, progress, diagnostics)",
            InjectScreening,
            async (mock, _, speedProgress) => await mock.GetFastestServerByLatencyAsync([Anywhere], speedProgress, null)
        },
        {
            "GetDownloadSpeedAsync(server)",
            InjectDownload,
            async (mock, _, _) => await mock.GetDownloadSpeedAsync(Anywhere)
        },
        {
            "GetDownloadSpeedAsync(server, progress)",
            InjectDownload,
            async (mock, _, speedProgress) => await mock.GetDownloadSpeedAsync(Anywhere, speedProgress)
        },
        {
            "GetDownloadSpeedAsync(server, progress, diagnostics)",
            InjectDownload,
            async (mock, _, speedProgress) => await mock.GetDownloadSpeedAsync(Anywhere, speedProgress, null)
        },
        {
            "GetUploadSpeedAsync(server)",
            InjectUpload,
            async (mock, _, _) => await mock.GetUploadSpeedAsync(Anywhere)
        },
        {
            "GetUploadSpeedAsync(server, progress)",
            InjectUpload,
            async (mock, _, speedProgress) => await mock.GetUploadSpeedAsync(Anywhere, speedProgress)
        },
        {
            "GetUploadSpeedAsync(server, progress, diagnostics)",
            InjectUpload,
            async (mock, _, speedProgress) => await mock.GetUploadSpeedAsync(Anywhere, speedProgress, null)
        },
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task EachOperation_WithAnInjectedFunction_ReturnsWhatThatFunctionReturned(string operation, Func<SpeedTestMock, object> inject, Func<SpeedTestMock, IProgress<LatencyTestProgress>, IProgress<SpeedTestProgress>, Task<object>> call)
    {
        // Given a mock whose function for this operation answers with a result this test holds.
        Assert.NotNull(operation);
        var mock = new SpeedTestMock();
        var injected = inject(mock);

        // When
        var answer = await call(mock, new NullProgress<LatencyTestProgress>(), new NullProgress<SpeedTestProgress>());

        // Then
        Assert.Same(injected, answer);
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task EachOperation_WithNoInjectedFunction_Throws(string operation, Func<SpeedTestMock, object> inject, Func<SpeedTestMock, IProgress<LatencyTestProgress>, IProgress<SpeedTestProgress>, Task<object>> call)
    {
        // Given a mock configured for nothing.
        Assert.NotNull(operation);
        Assert.NotNull(inject);
        var mock = new SpeedTestMock();

        // When, Then a consumer that forgot to configure the operation it calls is told so, rather
        // than handed a fabricated result.
        await Assert.ThrowsAsync<NotImplementedException>(() => call(mock, new NullProgress<LatencyTestProgress>(), new NullProgress<SpeedTestProgress>()));
    }

    [Fact]
    public async Task EachOperation_CalledWithAProgressReceiver_PassesItToTheInjectedFunction()
    {
        // Given a mock recording the progress receiver each function was handed.
        var handed = new List<object?>();
        var latencyProgress = new NullProgress<LatencyTestProgress>();
        var speedProgress = new NullProgress<SpeedTestProgress>();
        var mock = Recording(handed);

        // When
        await mock.GetServerLatencyAsync(Anywhere, latencyProgress);
        await mock.GetServerLatencyAsync(NamedUrl, latencyProgress);
        await mock.GetFastestServerByLatencyAsync([Anywhere], speedProgress);
        await mock.GetDownloadSpeedAsync(Anywhere, speedProgress);
        await mock.GetUploadSpeedAsync(Anywhere, speedProgress);

        // Then a consumer watching progress through the mock sees whatever its function reports.
        Assert.Equal([latencyProgress, latencyProgress, speedProgress, speedProgress, speedProgress], handed);
    }

    [Fact]
    public async Task EachOperation_CalledWithoutAProgressReceiver_HandsTheInjectedFunctionNone()
    {
        // Given
        var handed = new List<object?>();
        var mock = Recording(handed);

        // When
        await mock.GetServerLatencyAsync(Anywhere);
        await mock.GetServerLatencyAsync(NamedUrl);
        await mock.GetFastestServerByLatencyAsync([Anywhere]);
        await mock.GetDownloadSpeedAsync(Anywhere);
        await mock.GetUploadSpeedAsync(Anywhere);

        // Then a function can tell a caller that asked for no progress from one that did.
        Assert.All(handed, Assert.Null);
    }

    [Fact]
    public async Task EachOperation_CalledWithTheArgumentsAConsumerSupplies_PassesThemToTheInjectedFunction()
    {
        // Given
        var candidates = new IServer[] { Anywhere };
        IServer? latencyServer = null;
        string? latencyUrl = null;
        IServer[]? screened = null;
        IServer? downloadServer = null;
        IServer? uploadServer = null;
        var mock = new SpeedTestMock
        {
            GetServerLatencyAsyncFunc = (server, _, _) => { latencyServer = server; return Task.FromResult(Latency()); },
            GetServerLatencyByServerUrlAsyncFunc = (url, _, _) => { latencyUrl = url; return Task.FromResult(Latency()); },
            GetFastestServerByLatencyAsyncFunc = (servers, _, _) => { screened = servers; return Task.FromResult(Latency()); },
            GetDownloadSpeedAsyncFunc = (server, _, _) => { downloadServer = server; return Task.FromResult(Transfer()); },
            GetUploadSpeedAsyncFunc = (server, _, _) => { uploadServer = server; return Task.FromResult(Transfer()); },
        };

        // When
        await mock.GetServerLatencyAsync(Anywhere);
        await mock.GetServerLatencyAsync(NamedUrl);
        await mock.GetFastestServerByLatencyAsync(candidates);
        await mock.GetDownloadSpeedAsync(Anywhere);
        await mock.GetUploadSpeedAsync(Anywhere);

        // Then a function can answer differently per server, which is what makes the mock
        // configurable rather than fixed.
        Assert.Same(Anywhere, latencyServer);
        Assert.Equal(NamedUrl, latencyUrl);
        Assert.Same(candidates, screened);
        Assert.Same(Anywhere, downloadServer);
        Assert.Same(Anywhere, uploadServer);
    }

    [Fact]
    public async Task EachOperation_CalledWithACancellationToken_PassesItToTheInjectedFunction()
    {
        // Given
        using var source = new CancellationTokenSource();
        var handed = new List<CancellationToken>();
        var mock = new SpeedTestMock
        {
            GetServersAsyncFunc = token => { handed.Add(token); return Task.FromResult(Servers()); },
            GetServerLatencyAsyncFunc = (_, _, token) => { handed.Add(token); return Task.FromResult(Latency()); },
            GetServerLatencyByServerUrlAsyncFunc = (_, _, token) => { handed.Add(token); return Task.FromResult(Latency()); },
            GetFastestServerByLatencyAsyncFunc = (_, _, token) => { handed.Add(token); return Task.FromResult(Latency()); },
            GetDownloadSpeedAsyncFunc = (_, _, token) => { handed.Add(token); return Task.FromResult(Transfer()); },
            GetUploadSpeedAsyncFunc = (_, _, token) => { handed.Add(token); return Task.FromResult(Transfer()); },
        };

        // When
        await mock.GetServersAsync(source.Token);
        await mock.GetServerLatencyAsync(Anywhere, source.Token);
        await mock.GetServerLatencyAsync(NamedUrl, source.Token);
        await mock.GetFastestServerByLatencyAsync([Anywhere], source.Token);
        await mock.GetDownloadSpeedAsync(Anywhere, source.Token);
        await mock.GetUploadSpeedAsync(Anywhere, source.Token);

        // Then a function can honour cancellation, so a consumer can test a cancelled run.
        Assert.All(handed, token => Assert.Equal(source.Token, token));
    }

    [Fact]
    public async Task TheDiagnosticsOverloads_WithAnInjectedFunction_ReportNoRequests()
    {
        // Given behaviour comes from a function standing in for the whole measurement, so there is
        // nothing per-request for the mock to describe.
        var diagnostics = new List<RequestDiagnostic>();
        var receiver = new SynchronousProgress<RequestDiagnostic>(diagnostics.Add);
        var mock = Recording([]);

        // When
        await mock.GetServerLatencyAsync(Anywhere, null, receiver);
        await mock.GetServerLatencyAsync(NamedUrl, null, receiver);
        await mock.GetFastestServerByLatencyAsync([Anywhere], null, receiver);
        await mock.GetDownloadSpeedAsync(Anywhere, null, receiver);
        await mock.GetUploadSpeedAsync(Anywhere, null, receiver);

        // Then
        Assert.Empty(diagnostics);
    }

    // Each injector configures one operation and returns the result its function will produce, so a
    // test can assert the mock handed back that very object.

    private static object InjectServers(SpeedTestMock mock)
    {
        var servers = Servers();
        mock.GetServersAsyncFunc = _ => Task.FromResult(servers);
        return servers;
    }

    private static object InjectLatencyByServer(SpeedTestMock mock)
    {
        var latency = Latency();
        mock.GetServerLatencyAsyncFunc = (_, _, _) => Task.FromResult(latency);
        return latency;
    }

    private static object InjectLatencyByUrl(SpeedTestMock mock)
    {
        var latency = Latency();
        mock.GetServerLatencyByServerUrlAsyncFunc = (_, _, _) => Task.FromResult(latency);
        return latency;
    }

    private static object InjectScreening(SpeedTestMock mock)
    {
        var latency = Latency();
        mock.GetFastestServerByLatencyAsyncFunc = (_, _, _) => Task.FromResult(latency);
        return latency;
    }

    private static object InjectDownload(SpeedTestMock mock)
    {
        var download = Transfer();
        mock.GetDownloadSpeedAsyncFunc = (_, _, _) => Task.FromResult(download);
        return download;
    }

    private static object InjectUpload(SpeedTestMock mock)
    {
        var upload = Transfer();
        mock.GetUploadSpeedAsyncFunc = (_, _, _) => Task.FromResult(upload);
        return upload;
    }

    /// <summary>
    /// A mock whose every function records the progress receiver it was handed.
    /// </summary>
    private static SpeedTestMock Recording(List<object?> handed) => new()
    {
        GetServersAsyncFunc = _ => Task.FromResult(Servers()),
        GetServerLatencyAsyncFunc = (_, progress, _) => { handed.Add(progress); return Task.FromResult(Latency()); },
        GetServerLatencyByServerUrlAsyncFunc = (_, progress, _) => { handed.Add(progress); return Task.FromResult(Latency()); },
        GetFastestServerByLatencyAsyncFunc = (_, progress, _) => { handed.Add(progress); return Task.FromResult(Latency()); },
        GetDownloadSpeedAsyncFunc = (_, progress, _) => { handed.Add(progress); return Task.FromResult(Transfer()); },
        GetUploadSpeedAsyncFunc = (_, progress, _) => { handed.Add(progress); return Task.FromResult(Transfer()); },
    };

    private static IServer[] Servers() => [Anywhere];

    private static LatencyTestResult Latency() =>
        new() { Server = Anywhere, LatencyMilliseconds = 42, JitterMilliseconds = 4 };

    private static SpeedTestResult Transfer() =>
        new() { BytesProcessed = 4200, ElapsedMilliseconds = 1000, RequestsSucceeded = 7, RequestsFailed = 0 };
}
