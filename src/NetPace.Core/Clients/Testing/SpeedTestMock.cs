namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A mock implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// </summary>
public sealed class SpeedTestMock : ISpeedTestService
{
    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServersAsync"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<CancellationToken, Task<IServer[]>>? GetServersAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServerLatencyAsync(IServer, CancellationToken)"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer, CancellationToken, Task<ServerLatencyResult>>? GetServerLatencyAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServerLatencyAsync(IServer, Action{SpeedTestProgress}, CancellationToken)"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer, Action<SpeedTestProgress>, CancellationToken, Task<ServerLatencyResult>>? GetServerLatencyWithProgressAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServerLatencyAsync(string, CancellationToken)"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<string, CancellationToken, Task<ServerLatencyResult>>? GetServerLatencyByServerUrlAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServerLatencyAsync(string, Action{SpeedTestProgress}, CancellationToken)"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<string, Action<SpeedTestProgress>, CancellationToken, Task<ServerLatencyResult>>? GetServerLatencyByServerUrlWithProgressAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetFastestServerByLatencyAsync"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer[], CancellationToken, Task<ServerLatencyResult>>? GetFastestServerByLatencyAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for all <c>GetDownloadSpeedAsync</c> overloads.
    /// If null, the methods will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer, int, Action<SpeedTestProgress>, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for all <c>GetUploadSpeedAsync</c> overloads.
    /// If null, the methods will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
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
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyWithProgressAsyncFunc != null)
            return GetServerLatencyWithProgressAsyncFunc(server, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyByServerUrlAsyncFunc != null)
            return GetServerLatencyByServerUrlAsyncFunc(serverUrl, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyByServerUrlWithProgressAsyncFunc != null)
            return GetServerLatencyByServerUrlWithProgressAsyncFunc(serverUrl, UpdateProgress, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers, cancellationToken);
        throw new NotImplementedException(nameof(GetFastestServerByLatencyAsync));
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
