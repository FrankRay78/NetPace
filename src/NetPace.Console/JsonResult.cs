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
    /// Gets the measured latency to the server in milliseconds, or <see langword="null"/> when the
    /// latency test was not run.
    /// </summary>
    public long? LatencyMilliseconds { get; init; }

    /// <summary>
    /// Gets the measured download speed in bits per second, or <see langword="null"/> when the
    /// download test was not run. A number in a fixed unit, so a script reads the same value
    /// whatever the unit options say. An all-failed test reports <c>0</c>, not
    /// <see langword="null"/>; the counts distinguish it from a genuine zero-throughput
    /// measurement.
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
    /// Gets the measured upload speed in bits per second, or <see langword="null"/> when the upload
    /// test was not run. A number in a fixed unit, so a script reads the same value whatever the
    /// unit options say. An all-failed test reports <c>0</c>, not <see langword="null"/>; the
    /// counts distinguish it from a genuine zero-throughput measurement.
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

    /// <summary>
    /// Gets the label the user gave this machine, or <see langword="null"/> when none was given, in
    /// which case the field is left out — the shape JSON uses for what does not apply. A stable
    /// label of the user's own, so logs merged from several machines group and filter by it however
    /// a machine is renamed or readdressed.
    /// </summary>
    public string? DeviceId { get; init; }

    // User-defined fields

    /// <summary>
    /// Gets the user-defined pairs recorded for this run, or <see langword="null"/> when none were
    /// given, in which case the object is left out. Grouped in one object so a custom key can never
    /// replace a built-in field, and a key given more than once keeps the last value given.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomFields { get; init; }
}
