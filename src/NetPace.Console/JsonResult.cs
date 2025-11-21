namespace NetPace.Console;

/// <summary>
/// Represents a speed test result in JSON format.
/// </summary>
public sealed record JsonResult
{
    // Server

    /// <summary>
    /// Gets the geographic location of the test server.
    /// </summary>
    public required string ServerLocation { get; init; }

    /// <summary>
    /// Gets the sponsor name of the test server.
    /// </summary>
    public required string ServerSponsor { get; init; }

    /// <summary>
    /// Gets the URL of the test server.
    /// </summary>
    public required string ServerUrl { get; init; }

    // Speed test result

    /// <summary>
    /// Gets the timestamp when the speed test was performed.
    /// </summary>
    public required string Timestamp { get; init; }

    /// <summary>
    /// Gets the measured latency to the server.
    /// </summary>
    public required string Latency { get; init; }

    /// <summary>
    /// Gets the measured download speed.
    /// </summary>
    public required string DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the measured upload speed.
    /// </summary>
    public required string UploadSpeed { get; init; }
}
