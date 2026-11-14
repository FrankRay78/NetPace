using NetPace.Core.Settings;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Configuration settings for the Ookla speed test implementation.
/// </summary>
/// <remarks>
/// What every provider shares - latency, screening, the upload shape, and the download byte
/// ceiling and connection count - is on <see cref="SpeedTestSettings"/>. What is here is what only
/// an Ookla server has: the image sizes its download requests ask for, and the address its server
/// list is fetched from.
/// </remarks>
public sealed record OoklaSpeedtestSettings : SpeedTestSettings
{
    /// <summary>
    /// Gets the URL to retrieve the list of available Speedtest servers.
    /// </summary>
    /// <remarks>
    /// Defaults to the official Speedtest.net server list endpoint.
    /// </remarks>
    public string ServersUrl { get; init; } = "http://www.speedtest.net/speedtest-servers.php";

    /// <summary>
    /// A list of download sizes (in pixels) used to generate URLs for test files.
    /// </summary>
    /// <remarks>
    /// These sizes are used to create URLs in the form of <c>random{size}x{size}.jpg</c>
    /// to simulate different file sizes for measuring download throughput.
    /// </remarks>
    public int[] DownloadSizes { get; init; }

    /// <summary>
    /// The number of times to repeat the download for each size specified in <see cref="DownloadSizes"/>.
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
