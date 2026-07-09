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
    public Func<IServer, IProgress<LatencyTestProgress>?, CancellationToken, Task<LatencyTestResult>>? GetServerLatencyAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <see cref="GetServerLatencyAsync(string, CancellationToken)"/>.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<string, IProgress<LatencyTestProgress>?, CancellationToken, Task<LatencyTestResult>>? GetServerLatencyByServerUrlAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for <c>GetFastestServerByLatencyAsync</c> overloads.
    /// If null, the method will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer[], IProgress<SpeedTestProgress>?, CancellationToken, Task<LatencyTestResult>>? GetFastestServerByLatencyAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for all <c>GetDownloadSpeedAsync</c> overloads.
    /// If null, the methods will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer, IProgress<SpeedTestProgress>?, CancellationToken, Task<SpeedTestResult>>? GetDownloadSpeedAsyncFunc { get; set; }

    /// <summary>
    /// Gets or sets the delegate that provides behavior for all <c>GetUploadSpeedAsync</c> overloads.
    /// If null, the methods will throw <see cref="NotImplementedException"/> when called.
    /// </summary>
    public Func<IServer, IProgress<SpeedTestProgress>?, CancellationToken, Task<SpeedTestResult>>? GetUploadSpeedAsyncFunc { get; set; }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        if (GetServersAsyncFunc != null)
            return GetServersAsyncFunc(cancellationToken);
        throw new NotImplementedException(nameof(GetServersAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyAsyncFunc != null)
            return GetServerLatencyAsyncFunc(server, null, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyAsyncFunc != null)
            return GetServerLatencyAsyncFunc(server, progress, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyByServerUrlAsyncFunc != null)
            return GetServerLatencyByServerUrlAsyncFunc(serverUrl, null, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (GetServerLatencyByServerUrlAsyncFunc != null)
            return GetServerLatencyByServerUrlAsyncFunc(serverUrl, progress, cancellationToken);
        throw new NotImplementedException(nameof(GetServerLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers, null, cancellationToken);
        throw new NotImplementedException(nameof(GetFastestServerByLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (GetFastestServerByLatencyAsyncFunc != null)
            return GetFastestServerByLatencyAsyncFunc(servers, progress, cancellationToken);
        throw new NotImplementedException(nameof(GetFastestServerByLatencyAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, null, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (GetDownloadSpeedAsyncFunc != null)
            return GetDownloadSpeedAsyncFunc(server, progress, cancellationToken);
        throw new NotImplementedException(nameof(GetDownloadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, null, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        if (GetUploadSpeedAsyncFunc != null)
            return GetUploadSpeedAsyncFunc(server, progress, cancellationToken);
        throw new NotImplementedException(nameof(GetUploadSpeedAsync));
    }
}
