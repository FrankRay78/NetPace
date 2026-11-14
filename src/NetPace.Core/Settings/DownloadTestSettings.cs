namespace NetPace.Core.Settings;

/// <summary>
/// Settings for the download speed test that every provider shares: how much data the phase may
/// move in total, and how many transfers it runs at once.
/// </summary>
/// <remarks>
/// What each individual request asks for is the provider's own business and lives on its settings
/// record - an image size for Ookla, a MiB count for LibreSpeed - because the two speak different
/// protocols. The ceiling and the connection count are shared so that a profile means the same
/// amount of traffic and the same load whichever server is being tested.
/// </remarks>
public sealed record DownloadTestSettings
{
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
    /// Distinct from the per-request sizing on the provider's own settings record, which shapes
    /// each request. <see cref="DownloadSizeMb"/> caps the total run. The default
    /// <see cref="int.MaxValue"/> sentinel means "no cap".
    /// </remarks>
    public int DownloadSizeMb { get; init; } = int.MaxValue;
}
