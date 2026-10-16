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
    /// Gets the instant the speed test was performed, as local time carrying its offset from UTC.
    /// Serialized in ISO 8601 form, so a consumer reads one unambiguous instant whatever zone the
    /// machine that wrote it was in. Always a whole number of seconds, so the JSON and CSV forms of
    /// one run agree.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the measured latency to the server, or <see langword="null"/> when the latency test was
    /// not run.
    /// </summary>
    public string? Latency { get; init; }

    /// <summary>
    /// Gets the measured latency to the server in milliseconds, or <see langword="null"/> when the
    /// latency test was not run. Unlike <see cref="Latency"/>, which carries its unit in the
    /// string, this is a bare number a script can read without parsing.
    /// </summary>
    public long? LatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets the measured download speed, or <see langword="null"/> when the download test was not
    /// run. An all-failed test reports a zero speed, not <see langword="null"/>; the counts
    /// distinguish it from a genuine zero-throughput measurement.
    /// </summary>
    public string? DownloadSpeed { get; init; }

    /// <summary>
    /// Gets the measured download speed in bits per second, or <see langword="null"/> when the
    /// download test was not run. Unlike <see cref="DownloadSpeed"/>, this is a number in a fixed
    /// unit, so a script reads the same value whatever the unit options say. An all-failed test
    /// reports <c>0</c>, not <see langword="null"/>; the counts distinguish it from a genuine
    /// zero-throughput measurement.
    /// </summary>
    public long? DownloadSpeedBitsPerSecond { get; init; }

    /// <summary>
    /// Gets the number of download requests that succeeded, or <see langword="null"/> when the
    /// download test was not run.
    /// </summary>
    public int? DownloadSucceeded { get; init; }

    /// <summary>
    /// Gets the number of download requests that failed, or <see langword="null"/> when the
    /// download test was not run.
    /// </summary>
    public int? DownloadFailed { get; init; }

    /// <summary>
    /// Gets the measured upload speed, or <see langword="null"/> when the upload test was not run.
    /// An all-failed test reports a zero speed, not <see langword="null"/>; the counts distinguish
    /// it from a genuine zero-throughput measurement.
    /// </summary>
    public string? UploadSpeed { get; init; }

    /// <summary>
    /// Gets the measured upload speed in bits per second, or <see langword="null"/> when the upload
    /// test was not run. Unlike <see cref="UploadSpeed"/>, this is a number in a fixed unit, so a
    /// script reads the same value whatever the unit options say. An all-failed test reports
    /// <c>0</c>, not <see langword="null"/>; the counts distinguish it from a genuine
    /// zero-throughput measurement.
    /// </summary>
    public long? UploadSpeedBitsPerSecond { get; init; }

    /// <summary>
    /// Gets the number of upload requests that succeeded, or <see langword="null"/> when the upload
    /// test was not run.
    /// </summary>
    public int? UploadSucceeded { get; init; }

    /// <summary>
    /// Gets the number of upload requests that failed, or <see langword="null"/> when the upload
    /// test was not run.
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
