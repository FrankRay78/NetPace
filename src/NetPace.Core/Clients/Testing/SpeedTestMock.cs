namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A mock implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public sealed class SpeedTestMock : ISpeedTestService
{
    // Delegates for method behavior
    public Func<CancellationToken, Task<IServer[]>>? GetServersAsyncFunc { get; set; }
    public Func<IServer, CancellationToken, Task<ServerLatencyResult>>? GetServerLatencyAsyncFunc { get; set; }
    public Func<IServer[], CancellationToken, Task<ServerLatencyResult>>? GetFastestServerByLatencyAsyncFunc { get; set; }
    public Func<IServer, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedWithProgressAsyncFunc { get; set; }
    public Func<IServer, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedWithProgressAsyncFunc { get; set; }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        if (GetServersAsyncFunc != null)
            return GetServersAsyncFunc(cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyAsyncFunc != null)
            return GetServerLatencyAsyncFunc(server, cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers, cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedWithProgressAsyncFunc != null)
            return GetDownloadSpeedWithProgressAsyncFunc(server, UpdateProgress, cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, cancellationToken);
        throw new NotImplementedException();
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedWithProgressAsyncFunc != null)
            return GetUploadSpeedWithProgressAsyncFunc(server, UpdateProgress, cancellationToken);
        throw new NotImplementedException();
    }
}
