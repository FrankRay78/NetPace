using NetPace.Core.Clients.Ookla;
using System.Diagnostics;

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
    public Task<IServer[]> GetServersAsync()
    {
        return Task.FromResult(new IServer[]
        {
            new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
            new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
            new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
        });
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server)
    {
        var latencyResult = new ServerLatencyResult
        {
            Server = server,
            Latency = int.Parse(server.Location!.Replace("Location ", "")) * 100
        };

        return Task.FromResult<ServerLatencyResult>(latencyResult);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers)
    {
        var latencyResult = new ServerLatencyResult
        {
            Server = servers[0],
            Latency = int.Parse(servers[0].Location!.Replace("Location ", "")) * 100
        };

        return Task.FromResult<ServerLatencyResult>(latencyResult);
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

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 });
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server)
    {
        return GetUploadSpeedAsync(server, (_) => { });
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

        return Task.FromResult(new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 });
    }
}
