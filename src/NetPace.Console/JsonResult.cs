namespace NetPace.Console;

public sealed record JsonResult
{
    // Server
    public required string ServerLocation { get; init; }
    public required string ServerSponsor { get; init; }
    public required string ServerUrl { get; init; }

    // Speed test result
    public required string Timestamp { get; init; }
    public required string Latency { get; init; }
    public required string DownloadSpeed { get; init; }
    public required string UploadSpeed { get; init; }
}
