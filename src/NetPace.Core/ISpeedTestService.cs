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
    /// <returns>An array of available servers that can be used for speed testing.</returns>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the network latency (ping) to the specified server.
    /// </summary>
    /// <param name="server">The server to measure latency against.</param>
    /// <returns>The latency in milliseconds, or <c>null</c> if the latency could not be determined.</returns>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the fastest server based on latency from a given list of servers.
    /// </summary>
    /// <param name="servers">An array of servers to test for latency.</param>
    /// <returns>A tuple containing the server with the lowest latency and its latency in milliseconds,
    /// or <c>null</c> if no suitable server was found.</returns>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="UpdateProgress">An action that receives the download progress percentage (0 to 100).</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="UpdateProgress">An action that receives the upload progress percentage (0 to 100).</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default);
}