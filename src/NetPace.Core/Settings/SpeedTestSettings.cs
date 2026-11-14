using System.Net;

namespace NetPace.Core.Settings;

/// <summary>
/// The configuration every speed test provider shares: how latency is measured, how candidate
/// servers are screened, how much data each phase may move and at what concurrency, and which
/// proxy to go through.
/// </summary>
/// <remarks>
/// A provider's own settings record derives from this and adds what only that provider's protocol
/// needs - the shape of one download request, and where a server list is fetched from. The
/// per-profile data ceilings and connection counts live here, in <see cref="LoadFor"/>, so the
/// providers cannot drift apart: a profile means the same amount of traffic and the same load
/// whichever kind of server is being tested, while the request sizes that make up that traffic are
/// each provider's own.
/// <para>
/// Derived records are meant to live in <c>NetPace.Core</c>: the profile constructor is
/// <c>private protected</c>, so a consumer can accept and inspect this type. A record outside it
/// can still derive through the copy constructor C# gives every record, but gains nothing by it,
/// because every provider takes its own concrete settings record and
/// <see cref="SpeedTestProviderBase"/> cannot be extended from outside <c>NetPace.Core</c>. Such
/// a record is not supported: a member added here later, abstract or otherwise, may break it.
/// </para>
/// </remarks>
public abstract record SpeedTestSettings
{
    /// <summary>
    /// Gets the settings for screening candidate servers to choose one.
    /// </summary>
    public ServerScreeningSettings ServerScreening { get; init; } = new();

    /// <summary>
    /// Gets the settings for latency tests, idle and under load.
    /// </summary>
    public LatencyTestSettings LatencyTest { get; init; } = new();

    /// <summary>
    /// Gets the total-byte ceiling and connection count for download speed tests.
    /// </summary>
    public DownloadTestSettings DownloadTest { get; init; }

    /// <summary>
    /// Gets the settings for upload speed tests.
    /// </summary>
    public UploadTestSettings UploadTest { get; init; }

    /// <summary>
    /// Gets the network credentials for proxy authentication.
    /// </summary>
    public NetworkCredential? ProxyCredential { get; init; }

    /// <summary>
    /// Gets the proxy server address.
    /// </summary>
    public Uri? ProxyAddress { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use a proxy for HTTP requests.
    /// </summary>
    public bool UseProxy { get; init; }

    /// <summary>
    /// Populates the shared settings for the given profile.
    /// </summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    private protected SpeedTestSettings(Profile profile)
    {
        (DownloadTest, UploadTest) = LoadFor(profile);
    }

    /// <summary>
    /// The one table of per-profile data ceilings and connection counts, read by every provider's
    /// settings record so that a profile's traffic budget cannot differ between providers.
    /// </summary>
    /// <param name="profile">The traffic-load profile to materialise.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="profile"/> is not a defined <see cref="Profile"/> value.</exception>
    private static (DownloadTestSettings Download, UploadTestSettings Upload) LoadFor(Profile profile) => profile switch
    {
        Profile.Tiny => (
            new DownloadTestSettings { DownloadParallelTasks = 1, DownloadSizeMb = 1 },
            new UploadTestSettings { UploadSizeIncrementKb = 50, UploadIncrements = 1, UploadSizeIterations = 1, UploadParallelTasks = 1, UploadSizeMb = 1 }),

        Profile.Small => (
            new DownloadTestSettings { DownloadParallelTasks = 2, DownloadSizeMb = 10 },
            new UploadTestSettings { UploadSizeIncrementKb = 100, UploadIncrements = 4, UploadSizeIterations = 2, UploadParallelTasks = 2, UploadSizeMb = 2 }),

        Profile.Medium => (
            new DownloadTestSettings { DownloadParallelTasks = 4, DownloadSizeMb = 100 },
            new UploadTestSettings { UploadSizeIncrementKb = 200, UploadIncrements = 6, UploadSizeIterations = 5, UploadParallelTasks = 4, UploadSizeMb = 25 }),

        Profile.Large => (
            new DownloadTestSettings { DownloadParallelTasks = 16, DownloadSizeMb = 1024 },
            new UploadTestSettings { UploadSizeIncrementKb = 500, UploadIncrements = 8, UploadSizeIterations = 12, UploadParallelTasks = 16, UploadSizeMb = 256 }),

        Profile.Mega => (
            new DownloadTestSettings { DownloadParallelTasks = 32, DownloadSizeMb = 10240 },
            new UploadTestSettings { UploadSizeIncrementKb = 1024, UploadIncrements = 16, UploadSizeIterations = 16, UploadParallelTasks = 32, UploadSizeMb = 2048 }),

        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    };
}
