namespace NetPace.Core;

/// <summary>
/// Interface for performing internet speed tests.
/// </summary>
/// <remarks>
/// For download and upload tests, per-request network outcomes are <em>data</em>, not errors:
/// individual request failures (transport errors, timeouts, and non-success HTTP statuses) are
/// aggregated into the returned <see cref="SpeedTestResult"/>'s request counts
/// (<see cref="SpeedTestResult.RequestsAttempted"/>, <see cref="SpeedTestResult.RequestsSucceeded"/>,
/// and <see cref="SpeedTestResult.RequestsFailed"/>) rather than propagating to the caller — even
/// when every request fails. Callers detect an unusable measurement by inspecting the counts
/// (for example, <see cref="SpeedTestResult.RequestsSucceeded"/> is zero). Exceptions are reserved
/// for caller-requested cancellation and for genuine operational failures; they do not signal
/// network conditions.
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
    /// <returns>
    /// The result including bytes processed, elapsed time in milliseconds, and the per-request
    /// counts (attempted, succeeded, failed). Per-request network failures are reflected in the
    /// counts rather than thrown.
    /// </returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="progress">A progress reporter that receives download progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>
    /// The result including bytes processed, elapsed time in milliseconds, and the per-request
    /// counts (attempted, succeeded, failed). Per-request network failures are reflected in the
    /// counts rather than thrown.
    /// </returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>
    /// The result including bytes processed, elapsed time in milliseconds, and the per-request
    /// counts (attempted, succeeded, failed). Per-request network failures are reflected in the
    /// counts rather than thrown.
    /// </returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="progress">A progress reporter that receives upload progress updates.</param>
    /// <param name="cancellationToken">The token to allow the operation to be cancelled.</param>
    /// <returns>
    /// The result including bytes processed, elapsed time in milliseconds, and the per-request
    /// counts (attempted, succeeded, failed). Per-request network failures are reflected in the
    /// counts rather than thrown.
    /// </returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
}
