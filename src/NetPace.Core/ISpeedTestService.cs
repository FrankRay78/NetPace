namespace NetPace.Core;

/// <summary>
/// Interface for performing internet speed tests.
/// </summary>
public interface ISpeedTestService
{
    public Task<IServer[]> GetServersAsync();
    public Task<int?> GetServerLatencyAsync(IServer server);
    public Task<(IServer server, int latency)?> GetFastestServerByLatencyAsync(IServer[] servers);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server);

    /// <summary>
    /// Measures the download speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure download speed from.</param>
    /// <param name="UpdateProgress">An action that receives the download progress percentage (0 to 100).</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<int> UpdateProgress);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server);

    /// <summary>
    /// Measures the upload speed of the specified server.
    /// </summary>
    /// <param name="server">The server to measure upload speed from.</param>
    /// <param name="UpdateProgress">An action that receives the upload progress percentage (0 to 100).</param>
    /// <returns>The result including bytes processed and elapsed time in milliseconds.</returns>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<int> UpdateProgress);
}