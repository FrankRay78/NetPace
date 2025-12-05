using NetPace.Core.Clients.Ookla;

namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A stub implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public sealed class SpeedTestStub : ISpeedTestService
{
    private readonly IServer[] servers = new IServer[]
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
        var matched = servers.FirstOrDefault(s => s.Url.Equals(serverUrl));

        return matched != null
            ? int.Parse(matched.Sponsor!.Replace("Test Sponsor ", ""))
            : 10;
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(servers);
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
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] ignoredServers, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(ignoredServers, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] ignoredServers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
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

        // The fastest server in this stub is always the first one.
        var server = servers[0];

        var serverID = GetServerID(server.Url);

        var latencyResult = new LatencyTestResult
        {
            Server = server,
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
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, downloadSizeMb, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, int.MaxValue, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (progress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 25 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 50 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 75 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 100 });
        }

        var serverID = GetServerID(server.Url);

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 * serverID });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, uploadSizeMb, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, int.MaxValue, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (progress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 25 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 50 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 75 });
            Task.Delay(delayMilliseconds).Wait();
            progress.Report(new SpeedTestProgress { PercentageComplete = 100 });
        }

        var serverID = GetServerID(server.Url);

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 * serverID });
    }
}
