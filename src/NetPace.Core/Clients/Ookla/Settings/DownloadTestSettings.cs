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
    public int[] DownloadSizes { get; init; } = { 1500, 2000, 3000, 3500, 4000 };

    /// <summary>
    /// The number of times to repeat the download for each size specified in <see cref="DownloadSizes"/>.
    /// </summary>
    public int DownloadSizeIterations { get; init; } = 4;

    /// <summary>
    /// The number of parallel tasks used to download test data concurrently.
    /// </summary>
    public int DownloadParallelTasks { get; init; } = 8;

    /// <summary>
    /// Total-byte budget cap for the download phase, in IEC MiB. Once the running total of
    /// bytes returned across all parallel downloads reaches this value, the phase is cancelled:
    /// in-flight downloads are cancelled rather than awaited (their bytes are excluded) and no
    /// further downloads are scheduled. Actual bytes processed may still exceed the cap
    /// depending on parallelism and per-request size.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="DownloadSizes"/>, which sets the per-request pixel sizes
    /// (and therefore the per-request byte sizes). <see cref="DownloadSizeMb"/> caps the
    /// total run; <see cref="DownloadSizes"/> shapes each request. The default
    /// <see cref="int.MaxValue"/> sentinel means "no cap".
    /// </remarks>
    public int DownloadSizeMb { get; init; } = int.MaxValue;
}
