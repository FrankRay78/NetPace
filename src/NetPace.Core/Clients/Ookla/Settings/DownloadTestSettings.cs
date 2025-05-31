namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for the download speed test.
/// </summary>
public sealed record DownloadTestSettings
{
    /// <summary>
    /// A list of download sizes (in pixels) used to generate URLs for test files.
    /// </summary>
    /// <remarks>
    /// These sizes are used to create URLs in the form of <c>random{size}x{size}.jpg</c> 
    /// to simulate different file sizes for measuring download throughput.
    /// </remarks>
    public int[] DownloadSizes { get; set; } = { 1500, 2000, 3000, 3500, 4000 };

    /// <summary>
    /// The number of times to repeat the download for each size specified in <see cref="DownloadSizes"/>.
    /// </summary>
    public int DownloadSizeIterations { get; init; } = 4;

    /// <summary>
    /// The number of parallel tasks used to download test data concurrently.
    /// </summary>
    public int DownloadParallelTasks { get; init; } = 8;
}

