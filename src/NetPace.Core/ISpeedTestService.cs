namespace NetPace.Core;

/// <summary>
/// Interface for performing internet speed tests.
/// </summary>
/// <remarks>
/// Implementations of this interface should favor allowing network-related exceptions (e.g., timeouts, connection failures)
/// to propagate to the caller rather than catching and suppressing them. This approach enables consumers of the library
/// to implement their own error handling strategies that align with their application's needs.
/// </remarks>
public interface ISpeedTestService
{
    /// <summary>
    /// Retrieves a list of available test servers.
    /// </summary>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>An array of available servers that can be used for speed testing.</returns>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the network latency (ping) to the specified server.
    /// </summary>
    /// <param name="server">The server to measure latency against.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the network latency (ping) to the specified server.
    /// </summary>
    /// <param name="server">The server to measure latency against.</param>
    /// <param name="progress">A progress reporter that receives latency test progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the network latency (ping) to the specified server.
    /// </summary>
    /// <param name="serverUrl">The server to measure latency against.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the network latency (ping) to the specified server.
    /// </summary>
    /// <param name="serverUrl">The server URL to measure latency against.</param>
    /// <param name="progress">A progress reporter that receives latency test progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the fastest server based on latency from a given list of servers.
    /// </summary>
    /// <param name="servers">An array of servers to test for latency.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server with the lowest latency and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the fastest server based on latency from a given list of servers.
    /// </summary>
    /// <param name="servers">An array of servers to test for latency.</param>
    /// <param name="progress">A progress reporter that receives server selection progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The server with the lowest latency and its latency in milliseconds.</returns>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="downloadSizeMb">The size upon which to terminate the download test (IEC MiB).</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="progress">A progress reporter that receives download progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="downloadSizeMb">The size upon which to terminate the download test (IEC MiB).</param>
    /// <param name="progress">A progress reporter that receives download progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="uploadSizeMb">The size upon which to terminate the upload test (IEC MiB).</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="progress">A progress reporter that receives upload progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="uploadSizeMb">The size upon which to terminate the upload test (IEC MiB).</param>
    /// <param name="progress">A progress reporter that receives upload progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
}