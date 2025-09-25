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
    public Func<IServer, int, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedAsyncFunc { get; set; }
    public Func<IServer, int, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedAsyncFunc { get; set; }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        if (GetServersAsyncFunc != null)
            return GetServersAsyncFunc(cancellationToken);
        throw new NotImplementedException(nameof(GetServersAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyAsyncFunc != null)
            return GetServerLatencyAsyncFunc(server, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, int.MaxValue, _ => { }, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, downloadSizeMb, _ => { }, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, int.MaxValue, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, downloadSizeMb, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, int.MaxValue, _ => { }, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, uploadSizeMb, _ => { }, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, int.MaxValue, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, uploadSizeMb, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }
}
