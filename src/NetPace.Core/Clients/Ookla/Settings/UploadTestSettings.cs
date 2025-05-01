namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for uploading test data to measure upload speed.
/// </summary>
public sealed record UploadTestSettings
{
    public int UploadIncrements { get; init; } = 6;
    public int UploadParallelTasks { get; init; } = 8;
}