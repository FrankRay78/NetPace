namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A mock implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public sealed class SpeedTestMock : ISpeedTestService
{
    // Delegates for method behavior
    public Func<Task<IServer[]>>? GetServersAsyncFunc { get; set; }
    public Func<IServer, Task<ServerLatencyResult>>? GetServerLatencyAsyncFunc { get; set; }
    public Func<IServer[], Task<ServerLatencyResult>>? GetFastestServerByLatencyAsyncFunc { get; set; }
    public Func<IServer, Task<SpeedTestResult>>? GetDownloadSpeedAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, Task<SpeedTestResult>>? GetDownloadSpeedWithProgressAsyncFunc { get; set; }
    public Func<IServer, Task<SpeedTestResult>>? GetUploadSpeedAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, Task<SpeedTestResult>>? GetUploadSpeedWithProgressAsyncFunc { get; set; }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync()
    {
        if (GetServersAsyncFunc != null)
            return GetServersAsyncFunc();
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server)
    {
        if (GetServerLatencyAsyncFunc != null)
            return GetServerLatencyAsyncFunc(server);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress)
    {
        if (GetDownloadSpeedWithProgressAsyncFunc != null)
            return GetDownloadSpeedWithProgressAsyncFunc(server, UpdateProgress);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress)
    {
        if (GetUploadSpeedWithProgressAsyncFunc != null)
            return GetUploadSpeedWithProgressAsyncFunc(server, UpdateProgress);
        throw new NotImplementedException();
    }
}
