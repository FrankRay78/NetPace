using NetPace.Core.Settings;

namespace NetPace.Core.Clients.LibreSpeed;

/// <summary>
/// Configuration settings for the LibreSpeed speed test implementation.
/// </summary>
/// <remarks>
/// What every provider shares - latency, screening, the upload shape, and the download byte ceiling
/// and connection count - is on <see cref="SpeedTestSettings"/>, so a profile means the same
/// traffic budget and the same load here as it does for an Ookla run. There is no server feed
/// address: LibreSpeed publishes no server list NetPace reads, so a run always names its server.
/// </remarks>
public sealed record LibreSpeedtestSettings : SpeedTestSettings
{
    /// <summary>
    /// How many IEC MiB each download request asks <c>backend/garbage.php</c> for, as its
    /// <c>ckSize</c>.
    /// </summary>
    /// <remarks>
    /// LibreSpeed answers with exactly this many MiB, so it is the per-request size rather than a
    /// hint. The endpoint takes a whole number of MiB, substitutes its own default of 4 for zero or
    /// less and caps anything above 1024, so a value outside 1 to 1024 is rejected when the
    /// download runs. The total a download phase moves is bounded by
    /// <see cref="DownloadTestSettings.DownloadSizeMb"/>, not by this.
    /// </remarks>
    public int DownloadRequestSizeMib { get; init; }

    /// <summary>
    /// How many download requests are scheduled. The total-byte budget decides how many of them
    /// actually run. Zero or less is rejected when the download runs.
    /// </summary>
    /// <remarks>
    /// Per profile, enough that their sizes sum to at least the profile's ceiling, so the ceiling
    /// ends the phase rather than the request list running out, and at least as many as
    /// <see cref="DownloadTestSettings.DownloadParallelTasks"/>.
    /// </remarks>
    public int DownloadRequestCount { get; init; }

    /// <summary>
    /// Builds settings for the default profile (<see cref="Profile.Medium"/>).
    /// </summary>
    public LibreSpeedtestSettings() : this(Profile.Medium) { }

    /// <summary>
    /// Builds settings populated for the given profile.
    /// </summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    public LibreSpeedtestSettings(Profile profile) : base(profile)
    {
        // A small profile asks for little per request, so its ceiling is not overshot by a single
        // response; a large one asks for more, so a fast link is not spending its time on request
        // overhead. The ceilings and connection counts themselves are on SpeedTestSettings.
        (DownloadRequestSizeMib, DownloadRequestCount) = profile switch
        {
            Profile.Tiny => (1, 1),
            Profile.Small => (2, 8),
            Profile.Medium => (4, 32),
            Profile.Large => (8, 160),
            Profile.Mega => (16, 800),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}
