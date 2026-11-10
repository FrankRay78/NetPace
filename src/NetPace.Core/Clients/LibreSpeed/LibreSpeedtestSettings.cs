using NetPace.Core.Settings;

namespace NetPace.Core.Clients.LibreSpeed;

/// <summary>
/// Configuration settings for the LibreSpeed speed test implementation.
/// </summary>
/// <remarks>
/// What every provider shares - latency, screening, the upload shape, and the download byte
/// ceiling and connection count - is on <see cref="SpeedTestSettings"/>, so a profile means the
/// same traffic budget and the same load here as it does for an Ookla run. What is here is what
/// only a LibreSpeed server has: how much its <c>garbage.php</c> endpoint is asked for per request,
/// and how many such requests are scheduled.
/// <para>
/// There is no server feed address, because LibreSpeed publishes no server list NetPace reads: a
/// LibreSpeed run always names its server.
/// </para>
/// </remarks>
public sealed record LibreSpeedtestSettings : SpeedTestSettings
{
    /// <summary>
    /// How many IEC MiB each download request asks <c>backend/garbage.php</c> for, as its
    /// <c>ckSize</c>.
    /// </summary>
    /// <remarks>
    /// LibreSpeed answers <c>garbage.php</c> with exactly this many MiB, so it is the per-request
    /// size rather than a hint. The endpoint takes a whole number of MiB and clamps it to the range
    /// 1 to 1024, so a value outside that is answered with something else; the profiles stay inside
    /// it. The total a download phase moves is bounded by
    /// <see cref="DownloadTestSettings.DownloadSizeMb"/>, not by this.
    /// </remarks>
    public int DownloadRequestSizeMib { get; init; }

    /// <summary>
    /// How many download requests are scheduled. The total-byte budget decides how many of them
    /// actually run.
    /// </summary>
    /// <remarks>
    /// Enough of them, per profile, that their sizes sum to at least the profile's ceiling, so the
    /// ceiling is what ends the phase rather than the request list running out, and at least as
    /// many as <see cref="DownloadTestSettings.DownloadParallelTasks"/>, so the profile's
    /// connection count can be reached at all.
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
        // Each row schedules at least the profile's download ceiling, in requests of a size the
        // link can keep busy: a small profile asks for little per request so its ceiling is not
        // overshot by a single response, and a large one asks for more so a fast link is not
        // spending its time on request overhead. The ceilings themselves, and the connection
        // counts, come from the shared table on SpeedTestSettings.
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
