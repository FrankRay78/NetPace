namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A stub implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public sealed class SpeedTestStub : ISpeedTestService
{
    private int delayMilliseconds = 0;

    public SpeedTestStub() { }

    public SpeedTestStub(int delayMilliseconds)
    {
        this.delayMilliseconds = delayMilliseconds;
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new IServer[]
        {
            new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
            new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
            new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
        });
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        var latencyResult = new ServerLatencyResult
        {
            Server = server,
            Latency = int.Parse(server.Sponsor!.Replace("Test Sponsor ", "")) * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        var latencyResult = new ServerLatencyResult
        {
            Server = servers[0],
            Latency = int.Parse(servers[0].Sponsor!.Replace("Test Sponsor ", "")) * 100
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, _ => { });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
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

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 });
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

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 });
    }
}
