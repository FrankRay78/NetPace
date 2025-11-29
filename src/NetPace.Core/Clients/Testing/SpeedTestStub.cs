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
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(server, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, Action<LatencyTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (UpdateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new LatencyTestProgress { PercentageComplete = 25 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new LatencyTestProgress { PercentageComplete = 50 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new LatencyTestProgress { PercentageComplete = 75 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new LatencyTestProgress { PercentageComplete = 100 });
        }

        var serverID = GetServerID(server.Url);

        var latencyResult = new ServerLatencyResult
        {
            Server = server,
            Latency = serverID * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(serverUrl, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, Action<LatencyTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        var server = new Server() { Location = "(Unknown)", Sponsor = "(Unknown)", Url = serverUrl };

        return GetServerLatencyAsync(server, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] ignoredServers, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(ignoredServers, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] ignoredServers, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (UpdateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 33 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 66 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 100 });
        }

        // The fastest server in this stub is always the first one.
        var server = servers[0];

        var serverID = GetServerID(server.Url);

        var latencyResult = new ServerLatencyResult
        {
            Server = server,
            Latency = serverID * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, downloadSizeMb, _ => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, int.MaxValue, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (UpdateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 25 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 50 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 75 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 100 });
        }

        var serverID = GetServerID(server.Url);

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 * serverID });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, uploadSizeMb, (_) => { }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, int.MaxValue, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (UpdateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 25 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 50 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 75 });
            Task.Delay(delayMilliseconds).Wait();
            UpdateProgress(new SpeedTestProgress { PercentageComplete = 100 });
        }

        var serverID = GetServerID(server.Url);

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 * serverID });
    }
}
