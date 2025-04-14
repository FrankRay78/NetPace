namespace NetPace.Core.Clients;

/// <summary>
/// A stub implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public class SpeedTestStub : ISpeedTestService
{
    private int delayMilliseconds = 0;

    public SpeedTestStub() { }

    public SpeedTestStub(int delayMilliseconds)
    {
        this.delayMilliseconds = delayMilliseconds;
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync()
    {
        return Task.FromResult(new IServer[]
        {
            new Server { Name = "Test Server 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
            new Server { Name = "Test Server 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
            new Server { Name = "Test Server 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
        });
    }

    /// <inheritdoc/>
    public Task<int?> GetServerLatencyAsync(IServer server)
    {
        var latency = int.Parse(server.Name!.Replace("Test Server", "")) * 100;

        return Task.FromResult<int?>(latency);
    }

    /// <inheritdoc/>
    public Task<(IServer server, int latency)?> GetFastestServerByLatencyAsync(IServer[] servers)
    {
        var fastestServer = servers[0];
        var fastestLatency = int.Parse(fastestServer.Name!.Replace("Test Server", "")) * 100;

        return Task.FromResult<(IServer server, int latency)?>((fastestServer, fastestLatency));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server)
    {
        return GetDownloadSpeedAsync(server, _ => { });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<int> updateProgress)
    {
        if (updateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(25);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(50);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(75);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(100);
        }

        return Task.FromResult<SpeedTestResult>(new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server)
    {
        return GetUploadSpeedAsync(server, (int _) => { });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<int> updateProgress)
    {
        if (updateProgress is not null)
        {
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(25);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(50);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(75);
            Task.Delay(delayMilliseconds).Wait();
            updateProgress(100);
        }

        return Task.FromResult<SpeedTestResult>(new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 });
    }
}
