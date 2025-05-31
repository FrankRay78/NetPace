namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for the upload speed test.
/// </summary>
public sealed record UploadTestSettings
{
    /// <summary>
    /// The number of incremental upload sizes to generate for the test.
    /// </summary>
    /// <remarks>
    /// Each increment increases the upload payload size by 200KB. For example, a value of 6 generates
    /// upload blocks of 200KB, 400KB, ..., up to 1.2MB. Each block is uploaded multiple times during the test.
    /// </remarks>
    public int UploadIncrements { get; init; } = 6;

    /// <summary>
    /// The number of parallel tasks used to upload test data concurrently.
    /// </summary>
    public int UploadParallelTasks { get; init; } = 8;
}
