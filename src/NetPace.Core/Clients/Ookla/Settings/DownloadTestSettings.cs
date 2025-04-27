namespace NetPace.Core.Clients.Ookla.Settings;

/// <summary>
/// Settings for downloading test data to measure download speed.
/// </summary>
public sealed record DownloadTestSettings
{
    // These are used to generate the url for downloading test files.
    // eg: random1500x1500.jpg
    public int[] DownloadSizes { get; set; } = { 1500, 2000, 3000, 3500, 4000 };

    public int DownloadSizeIterations { get; set; } = 4;
    public int DownloadParallelTasks { get; set; } = 8;
}
