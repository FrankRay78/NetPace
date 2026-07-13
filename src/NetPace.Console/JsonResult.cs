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
    /// Gets the measured download speed, or <see langword="null"/> when every download request
    /// failed (no valid measurement) or the download dimension was not run.
    /// </summary>
    public required string DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the number of download requests that succeeded, or <see langword="null"/> when the
    /// download dimension was not run.
    /// </summary>
    public int? DownloadSucceeded { get; init; }

    /// <summary>
    /// Gets the number of download requests that failed, or <see langword="null"/> when the
    /// download dimension was not run.
    /// </summary>
    public int? DownloadFailed { get; init; }

    /// <summary>
    /// Gets the measured upload speed, or <see langword="null"/> when every upload request failed
    /// (no valid measurement) or the upload dimension was not run.
    /// </summary>
    public required string UploadSpeed { get; init; }

    /// <summary>
    /// Gets the number of upload requests that succeeded, or <see langword="null"/> when the upload
    /// dimension was not run.
    /// </summary>
    public int? UploadSucceeded { get; init; }

    /// <summary>
    /// Gets the number of upload requests that failed, or <see langword="null"/> when the upload
    /// dimension was not run.
    /// </summary>
    public int? UploadFailed { get; init; }

    // Device identity

    /// <summary>
    /// Gets the IP address of the device running the speed test.
    /// </summary>
    public required string IPAddress { get; init; }

    /// <summary>
    /// Gets the hostname of the device running the speed test.
    /// </summary>
    public required string Hostname { get; init; }
}
