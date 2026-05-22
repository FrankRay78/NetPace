using System.Net;
using NetPace.Core.Clients.Ookla.Settings;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Configuration settings for the Ookla speed test implementation.
/// </summary>
public sealed record OoklaSpeedtestSettings
{
    // Test-specific settings

    /// <summary>
    /// Gets or sets the settings for discovering available speed test servers.
    /// </summary>
    public ServerDiscoverySettings ServerDiscovery { get; init; } = new();

    /// <summary>
    /// Gets or sets the settings for latency tests.
    /// </summary>
    public LatencyTestSettings LatencyTest { get; init; } = new();

    /// <summary>
    /// Gets or sets the settings for download speed tests.
    /// </summary>
    public DownloadTestSettings DownloadTest { get; init; }

    /// <summary>
    /// Gets or sets the settings for upload speed tests.
    /// </summary>
    public UploadTestSettings UploadTest { get; init; }

    // Network options

    /// <summary>
    /// Gets or sets the network credentials for proxy authentication.
    /// </summary>
    public NetworkCredential? ProxyCredential { get; init; }

    /// <summary>
    /// Gets or sets the proxy server address.
    /// </summary>
    public Uri? ProxyAddress { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to use a proxy for HTTP requests.
    /// </summary>
    public bool UseProxy { get; init; }

    /// <summary>
    /// Builds settings for the default profile (<see cref="Profile.Medium"/>).
    /// </summary>
    public OoklaSpeedtestSettings() : this(Profile.Medium) { }

    /// <summary>
    /// Builds settings populated for the given profile.
    /// </summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    public OoklaSpeedtestSettings(Profile profile)
    {
        (DownloadTest, UploadTest) = profile switch
        {
            Profile.Tiny => (
                new DownloadTestSettings { DownloadSizes = new[] { 350 }, DownloadSizeIterations = 1, DownloadParallelTasks = 1, DownloadSizeMb = 1 },
                new UploadTestSettings { UploadSizeIncrementKb = 50, UploadIncrements = 1, UploadSizeIterations = 1, UploadParallelTasks = 1, UploadSizeMb = 1 }),

            Profile.Small => (
                new DownloadTestSettings { DownloadSizes = new[] { 1000, 1500 }, DownloadSizeIterations = 2, DownloadParallelTasks = 2, DownloadSizeMb = 10 },
                new UploadTestSettings { UploadSizeIncrementKb = 100, UploadIncrements = 4, UploadSizeIterations = 2, UploadParallelTasks = 2, UploadSizeMb = 2 }),

            Profile.Medium => (
                new DownloadTestSettings { DownloadSizes = new[] { 1500, 2000, 3000, 3500, 4000 }, DownloadSizeIterations = 2, DownloadParallelTasks = 4, DownloadSizeMb = 100 },
                new UploadTestSettings { UploadSizeIncrementKb = 200, UploadIncrements = 6, UploadSizeIterations = 5, UploadParallelTasks = 4, UploadSizeMb = 25 }),

            Profile.Large => (
                new DownloadTestSettings { DownloadSizes = new[] { 2000, 2500, 3000, 3500, 4000 }, DownloadSizeIterations = 12, DownloadParallelTasks = 16, DownloadSizeMb = 1024 },
                new UploadTestSettings { UploadSizeIncrementKb = 500, UploadIncrements = 8, UploadSizeIterations = 12, UploadParallelTasks = 16, UploadSizeMb = 256 }),

            Profile.Mega => (
                new DownloadTestSettings { DownloadSizes = new[] { 3000, 4000, 5000, 6000, 7000 }, DownloadSizeIterations = 40, DownloadParallelTasks = 32, DownloadSizeMb = 10240 },
                new UploadTestSettings { UploadSizeIncrementKb = 1024, UploadIncrements = 16, UploadSizeIterations = 16, UploadParallelTasks = 32, UploadSizeMb = 2048 }),

            _ => throw new ArgumentOutOfRangeException(nameof(profile)),
        };
    }
}
