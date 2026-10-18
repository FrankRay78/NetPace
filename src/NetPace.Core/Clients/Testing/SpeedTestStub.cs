using NetPace.Core.Clients.Ookla;

namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A stub implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
/// <remarks>
/// The diagnostics overloads report nothing: the stub fabricates aggregate results rather than issuing requests, so there are no per-request diagnostics to report.
/// </remarks>
public sealed class SpeedTestStub : ISpeedTestService
{
    private readonly IServer[] stubServers = new IServer[]
    {
        new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
        new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
        new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
    };

    private readonly int delayMilliseconds = 0;

    /// <summary>
    /// Constructs a new <see cref="SpeedTestStub"/> instance.
    /// </summary>
    public SpeedTestStub() { }

    /// <summary>
    /// Constructs a new <see cref="SpeedTestStub"/> instance with a specified delay for progress updates.
    /// </summary>
    public SpeedTestStub(int delayMilliseconds)
    {
        this.delayMilliseconds = delayMilliseconds;
    }

    private int GetServerID(string serverUrl)
    {
        // First see if we can match the server on our 'pre-canned list'
        var matched = stubServers.FirstOrDefault(s => s.Url.Equals(serverUrl));

        return matched != null
            ? int.Parse(matched.Sponsor!.Replace("Test Sponsor ", ""))
            : 10;
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(stubServers);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(server, new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 25 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 50 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 75 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 100 });

        var serverID = GetServerID(server.Url);

        var latencyResult = new LatencyTestResult
        {
            Server = server,
            LatencyMilliseconds = serverID * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(serverUrl, new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var server = new Server() { Location = "(Unknown)", Sponsor = "(Unknown)", Url = serverUrl };

        return GetServerLatencyAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(servers, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (progress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 33 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 66 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 100 });
        }

        // Every candidate offered is screened and the lowest-numbered one wins. Callers screen a
        // single candidate through this method as well as a list, so the winner is taken from the
        // array given rather than from the stub's own server list.
        var fastest = servers.OrderBy(candidate => GetServerID(candidate.Url)).First();

        var serverID = GetServerID(fastest.Url);

        var latencyResult = new LatencyTestResult
        {
            Server = fastest,
            LatencyMilliseconds = serverID * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var serverID = GetServerID(server.Url);

        var result = new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 * serverID, RequestsSucceeded = 150, RequestsFailed = 0 };

        ReportClimbTo(progress, result);

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var serverID = GetServerID(server.Url);

        var result = new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 * serverID, RequestsSucceeded = 32, RequestsFailed = 0 };

        ReportClimbTo(progress, result);

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reports progress towards <paramref name="result"/> in four updates, the last of them the
    /// result itself.
    /// </summary>
    /// <remarks>
    /// Bytes trail the clock early on and catch it up, so a caller showing the average speed since
    /// the test began watches a figure that climbs and then settles on exactly the speed the
    /// returned result reports — the convergence a real measurement shows.
    /// </remarks>
    private void ReportClimbTo(IProgress<SpeedTestProgress>? progress, SpeedTestResult result)
    {
        if (progress is null)
        {
            return;
        }

        (int percentageComplete, double bytesSoFar)[] updates =
        [
            (25, 0.10),
            (50, 0.35),
            (75, 0.65),
            (100, 1.00),
        ];

        foreach (var (percentageComplete, bytesSoFar) in updates)
        {
            Task.Delay(delayMilliseconds).Wait();

            progress.Report(new SpeedTestProgress
            {
                PercentageComplete = percentageComplete,
                BytesProcessed = (long)(result.BytesProcessed * bytesSoFar),
                ElapsedMilliseconds = result.ElapsedMilliseconds * percentageComplete / 100
            });
        }
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(server, progress ?? new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(serverUrl, progress ?? new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(servers, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }
}
