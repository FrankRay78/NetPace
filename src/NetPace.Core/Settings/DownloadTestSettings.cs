namespace NetPace.Core.Settings;

/// <summary>
/// Settings for the download speed test that every provider shares: how much data the phase may
/// move in total, and how many transfers it runs at once.
/// </summary>
/// <remarks>
/// What each individual request asks for lives on the provider's own settings record - an image
/// size for Ookla, a MiB count for LibreSpeed. The ceiling and the connection count are shared, so
/// a profile means the same traffic and the same load whichever server is being tested.
/// </remarks>
public sealed record DownloadTestSettings
{
    /// <summary>
    /// The number of parallel tasks used to download test data concurrently.
    /// </summary>
    public int DownloadParallelTasks { get; init; } = 8;

    /// <summary>
    /// Total-byte budget cap for the download phase, in IEC MiB. Once the bytes returned by the
    /// downloads that completed successfully reach this value, the phase is cancelled: in-flight
    /// downloads are cancelled rather than awaited, and no further downloads are scheduled. What
    /// those in-flight downloads had already received still counts towards the reported figure,
    /// so actual bytes processed may exceed the cap, by more the more parallel the phase is.
    /// </summary>
    /// <remarks>
    /// Distinct from the per-request sizing on the provider's own settings record, which shapes
    /// each request. <see cref="DownloadSizeMb"/> caps the total run. The default
    /// <see cref="int.MaxValue"/> sentinel means "no cap".
    /// </remarks>
    public int DownloadSizeMb { get; init; } = int.MaxValue;
}
