using NetPace.Core.Settings;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Configuration settings for the Ookla speed test implementation.
/// </summary>
/// <remarks>
/// What every provider shares - latency, screening, the upload shape, and the download byte ceiling
/// and connection count - is on <see cref="SpeedTestSettings"/>.
/// </remarks>
public sealed record OoklaSpeedtestSettings : SpeedTestSettings
{
    /// <summary>
    /// The address the list of available Speedtest servers is fetched from.
    /// </summary>
    public string ServersUrl { get; init; } = "http://www.speedtest.net/speedtest-servers.php";

    /// <summary>
    /// The download sizes, in pixels, that the <c>random{size}x{size}.jpg</c> requests ask for.
    /// </summary>
    public int[] DownloadSizes { get; init; }

    /// <summary>
    /// How many times each size in <see cref="DownloadSizes"/> is requested.
    /// </summary>
    public int DownloadSizeIterations { get; init; }

    /// <summary>
    /// Builds settings for the default profile (<see cref="Profile.Medium"/>).
    /// </summary>
    public OoklaSpeedtestSettings() : this(Profile.Medium) { }

    /// <summary>
    /// Builds settings populated for the given profile.
    /// </summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    public OoklaSpeedtestSettings(Profile profile) : base(profile)
    {
        (DownloadSizes, DownloadSizeIterations) = profile switch
        {
            Profile.Tiny => (new[] { 350 }, 1),
            Profile.Small => (new[] { 1000, 1500 }, 2),
            Profile.Medium => (new[] { 1500, 2000, 3000, 3500, 4000 }, 2),
            Profile.Large => (new[] { 2000, 2500, 3000, 3500, 4000 }, 12),
            Profile.Mega => (new[] { 3000, 4000, 5000, 6000, 7000 }, 40),
            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}
