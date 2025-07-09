namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for the upload speed test.
/// </summary>
public sealed record UploadTestSettings
{
    /// <summary>
    /// The size step in kilobytes used to grow the payload size per increment.
    /// </summary>
    /// <remarks>
    /// This value defines how much larger each successive payload is compared to the previous one.
    /// </remarks>
    public int UploadSizeIncrementKb { get; init; } = 200;

    /// <summary>
    /// The number of incremental upload sizes to generate for the test.
    /// </summary>
    /// <remarks>
    /// Each increment increases the payload size by <see cref="UploadSizeIncrementKb"/>. For example, if set to 6 and 
    /// <c>BaseSizeKb</c> is 200, it generates sizes of 200KB, 400KB, ..., up to 1.2MB.
    /// </remarks>
    public int UploadIncrements { get; init; } = 6;

    /// <summary>
    /// The number of times to repeat the upload for each size.
    /// </summary>
    /// <remarks>
    /// Repeated payloads simulate uploading multiple chunks of the same size and improve sample accuracy.
    /// </remarks>
    public int UploadSizeIterations { get; init; } = 10;

    /// <summary>
    /// The number of parallel tasks used to upload test data concurrently.
    /// </summary>
    public int UploadParallelTasks { get; init; } = 8;
}
