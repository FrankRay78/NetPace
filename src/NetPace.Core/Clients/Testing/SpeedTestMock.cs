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
    public Func<IServer, int, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedWithDownloadSizeAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedWithProgressAsyncFunc { get; set; }
    public Func<IServer, int, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedWithDownloadSizeAndProgressAsyncFunc { get; set; }
    public Func<IServer, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedAsyncFunc { get; set; }
    public Func<IServer, int, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedWithUploadSizeAsyncFunc { get; set; }
    public Func<IServer, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedWithProgressAsyncFunc { get; set; }
    public Func<IServer, int, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedWithUploadSizeAndProgressAsyncFunc { get; set; }

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
            return GetDownloadSpeedAsyncFunc(server, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedWithDownloadSizeAsyncFunc != null)
            return GetDownloadSpeedWithDownloadSizeAsyncFunc(server, downloadSizeMb, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedWithProgressAsyncFunc != null)
            return GetDownloadSpeedWithProgressAsyncFunc(server, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedWithDownloadSizeAndProgressAsyncFunc != null)
            return GetDownloadSpeedWithDownloadSizeAndProgressAsyncFunc(server, downloadSizeMb, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedWithUploadSizeAsyncFunc != null)
            return GetUploadSpeedWithUploadSizeAsyncFunc(server, uploadSizeMb, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedWithProgressAsyncFunc != null)
            return GetUploadSpeedWithProgressAsyncFunc(server, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedWithUploadSizeAndProgressAsyncFunc != null)
            return GetUploadSpeedWithUploadSizeAndProgressAsyncFunc(server, uploadSizeMb, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }
}
