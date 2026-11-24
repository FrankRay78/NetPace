using System.Net;

namespace NetPace.Core.Clients.LibreSpeed;

/// <summary>
/// A LibreSpeed implementation of the <see cref="ISpeedTestService"/> interface: the open-source
/// test server anyone can host, named by the address it is installed at.
/// </summary>
/// <remarks>
/// The measurement itself lives in <see cref="SpeedTestProviderBase"/>, so a LibreSpeed run reports
/// every figure an Ookla run does. What is here is the two endpoints inside the install
/// (<c>backend/empty.php</c> for the ping and for uploads, <c>backend/garbage.php</c> for
/// downloads) and the empty body the ping endpoint answers with.
/// <para>
/// A LibreSpeed run always names its server: <see cref="GetServersAsync"/> is not supported.
/// Screening a caller-supplied list works as it does for Ookla.
/// </para>
/// </remarks>
public sealed class LibreSpeedtest : SpeedTestProviderBase
{
    private const string PingPath = "backend/empty.php";
    private const string GarbagePath = "backend/garbage.php";

    /// <summary>The largest <c>ckSize</c> <c>garbage.php</c> honours; it caps anything above.</summary>
    private const int MaximumDownloadRequestSizeMib = 1024;

    // The extensions a user copies from the browser's address bar in place of the install's
    // directory.
    private static readonly string[] PageExtensions = [".html", ".htm", ".php"];

    private readonly LibreSpeedtestSettings settings;

    /// <summary>
    /// Constructs a new instance of the <see cref="LibreSpeedtest"/> class.
    /// </summary>
    /// <param name="speedtestSettings">Per-request sizing, iteration and parallelism settings; the defaults are used when omitted.</param>
    /// <param name="httpClientOverride">An HTTP client to use in place of the one this class would construct.</param>
    /// <param name="delayProviderOverride">A delay provider to use in place of real waiting.</param>
    /// <param name="timeProviderOverride">
    /// The clock every <c>ElapsedMilliseconds</c> and <see cref="RequestDiagnostic.StartedAt"/> is
    /// read from; the system clock when omitted. Waiting is not measurement and stays on the system
    /// clock regardless: the screening ceiling, the per-request HTTP timeouts, and the pause between
    /// latency probes that <paramref name="delayProviderOverride"/> governs.
    /// <para>
    /// A clock supplied here must override <see cref="TimeProvider.GetTimestamp"/> and
    /// <see cref="TimeProvider.TimestampFrequency"/>, not <see cref="TimeProvider.GetUtcNow"/>
    /// alone, and must be monotonic; see <see cref="TimeProvider.GetTimestamp"/>.
    /// </para>
    /// </param>
    public LibreSpeedtest(LibreSpeedtestSettings? speedtestSettings = null, HttpClient? httpClientOverride = null, IDelayProvider? delayProviderOverride = null, TimeProvider? timeProviderOverride = null)
        : base(speedtestSettings ?? new LibreSpeedtestSettings(), httpClientOverride, delayProviderOverride, timeProviderOverride)
    {
        settings = (LibreSpeedtestSettings)SharedSettings;
    }

    /// <summary>
    /// Not supported: LibreSpeed publishes no server list NetPace reads, so a LibreSpeed run always
    /// names its server.
    /// </summary>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            $"{nameof(LibreSpeedtest)} discovers no servers: LibreSpeed publishes no server list, so the server to test must be named by its URL. " +
            $"Screening a list you supply yourself works as it does for any provider - see {nameof(GetFastestServerByLatencyAsync)}.");

    /// <inheritdoc/>
    private protected override string LatencyProbeUrl(string serverUrl) => InstallDirectory(serverUrl) + PingPath;

    // An upload already goes to the ping endpoint, so its resolved address is probed as it stands:
    // joining the ping path onto it again would probe a file that does not exist. A download's
    // transfer address is the install's own, which the path is joined onto as for the idle probe.
    private protected override string LoadedLatencyProbeUrl(IServer server, string transferUrl) =>
        new Uri(transferUrl, UriKind.Absolute).AbsolutePath.EndsWith("/" + PingPath, StringComparison.Ordinal)
            ? transferUrl
            : LatencyProbeUrl(transferUrl);

    // An empty body is the only part of empty.php's answer that distinguishes a LibreSpeed install
    // from a plain web server serving a page at the same address: its content type is text/html
    // either way. A non-success status, which is what an Ookla server gives, has already failed the
    // request before it reaches here.
    private protected override bool IsLatencyResponseValid(string response) => response.Length == 0;

    // The user names a LibreSpeed server, so a 4xx almost always means the address is wrong - the
    // web root of an install under a path, or an Ookla server, answers exactly that - and gets the
    // same guidance as an address answering with a page. A 5xx says the opposite: something at the
    // right address is failing, often a proxy whose backend is down.
    private protected override string LatencyStatusFailureMessage(string probeUrl, HttpStatusCode status) =>
        (int)status is >= 400 and < 500
            ? LatencyResponseRejectedMessage(probeUrl)
            : $"The server at {probeUrl} answered with HTTP status {(int)status}, so it could not be measured. " +
              "Check that the LibreSpeed server, and any proxy in front of it, is running.";

    /// <inheritdoc/>
    private protected override string LatencyResponseRejectedMessage(string probeUrl) =>
        $"The server at {probeUrl} answered, but not as a LibreSpeed server. " +
        "Check that the server URL names the address LibreSpeed is installed at, with its path if it is not at the web root.";

    /// <inheritdoc/>
    /// <example>
    /// http://pi.local:8080/backend/garbage.php?ckSize=4&amp;r=0
    /// http://pi.local:8080/backend/garbage.php?ckSize=4&amp;r=1
    /// ...
    /// </example>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <see cref="LibreSpeedtestSettings.DownloadRequestSizeMib"/> is outside 1 to 1024, which
    /// <c>garbage.php</c> would answer with a different size, or
    /// <see cref="LibreSpeedtestSettings.DownloadRequestCount"/> is zero or less.
    /// </exception>
    private protected override IEnumerable<string> DownloadRequestUrls(string serverUrl)
    {
        // Checked before any request is made rather than as the sequence is read, so a request
        // shape the server would not honour fails the call instead of being measured.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.DownloadRequestSizeMib, nameof(settings.DownloadRequestSizeMib));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(settings.DownloadRequestSizeMib, MaximumDownloadRequestSizeMib, nameof(settings.DownloadRequestSizeMib));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.DownloadRequestCount, nameof(settings.DownloadRequestCount));

        var garbageUrl = InstallDirectory(serverUrl) + GarbagePath;
        var requestSizeMib = settings.DownloadRequestSizeMib;
        var requestCount = settings.DownloadRequestCount;

        return Requests();

        IEnumerable<string> Requests()
        {
            for (var request = 0; request < requestCount; request++)
            {
                // r is a cache-buster, as it is on the Ookla side: a proxy that served one of these
                // twice would measure its own disk rather than the link.
                yield return $"{garbageUrl}?ckSize={requestSizeMib}&r={request}";
            }
        }
    }

    // LibreSpeed accepts a streamed body at the same address it answers the ping on.
    private protected override string UploadUrl(string serverUrl) => InstallDirectory(serverUrl) + PingPath;

    // The prefix the endpoint paths are joined onto. The rest of the given path is kept, which is
    // what makes an install under a path work: resolving "." against the address instead, as the
    // Ookla provider does, would turn "http://nas.local/speedtest" into "http://nas.local/" and
    // test the web root. So only a last segment that is plainly a page is dropped, and a trailing
    // slash added where the user left it off, so every form of the address tests the same server.
    //
    // "http://nas.local/speedtest"            -> "http://nas.local/speedtest/"
    // "http://pi.local:8080/index.html?lang=en" -> "http://pi.local:8080/"
    private static string InstallDirectory(string serverUrl)
    {
        var uri = new Uri(serverUrl, UriKind.Absolute);
        var path = uri.GetLeftPart(UriPartial.Path);

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        if (PageExtensions.Any(extension => lastSegment.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return path[..^lastSegment.Length];
        }

        return path.EndsWith('/') ? path : path + "/";
    }
}
