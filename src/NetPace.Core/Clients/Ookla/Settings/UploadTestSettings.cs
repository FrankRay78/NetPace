namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for uploading test data to measure upload speed.
/// </summary>
public sealed class UploadTestSettings
{
    public int UploadIncrements { get; set; } = 6;
    public int UploadParallelTasks { get; set; } = 8;
}