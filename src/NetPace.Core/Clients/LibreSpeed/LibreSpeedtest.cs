using System.Net;

namespace NetPace.Core.Clients.LibreSpeed;

/// <summary>
/// A LibreSpeed implementation of the <see cref="ISpeedTestService"/> interface: the open-source
/// test server anyone can host, named by the address it is installed at.
/// </summary>
/// <remarks>
/// The measurement itself lives in <see cref="SpeedTestProviderBase"/>, shared with every other
/// provider, so a LibreSpeed run reports every figure an Ookla run does. What is here is what a
/// LibreSpeed server needs that an Ookla one does not: the two endpoints inside the install
/// (<c>backend/empty.php</c> for the ping and for uploads, <c>backend/garbage.php</c> for
/// downloads) and the empty body the ping endpoint answers with.
/// <para>
/// A LibreSpeed run always names its server: <see cref="GetServersAsync"/> is not supported,
/// because LibreSpeed publishes no server list NetPace reads. Screening a caller-supplied list
/// works as it does for Ookla.
/// </para>
/// </remarks>
public sealed class LibreSpeedtest : SpeedTestProviderBase
{
    private const string PingPath = "backend/empty.php";
    private const string GarbagePath = "backend/garbage.php";

    /// <summary>The largest <c>ckSize</c> <c>garbage.php</c> honours; it caps anything above.</summary>
    private const int MaximumDownloadRequestSizeMib = 1024;

    /// <summary>
    /// The extensions of a page a browser shows at an install's address, which a user may copy in
    /// place of the install's directory.
    /// </summary>
    private static readonly string[] PageExtensions = [".html", ".htm", ".php"];

    private readonly LibreSpeedtestSettings settings;

    /// <summary>
    /// Constructs a new instance of the <see cref="LibreSpeedtest"/> class.
    /// </summary>
    /// <param name="speedtestSettings">Per-request sizing, iteration and parallelism settings; the defaults are used when omitted.</param>
    /// <param name="httpClientOverride">An HTTP client to use in place of the one this class would construct.</param>
    /// <param name="delayProviderOverride">A delay provider to use in place of real waiting.</param>
    /// <param name="timeProviderOverride">
    /// The clock every elapsed time is measured on - every <c>ElapsedMilliseconds</c> this class
    /// reports, on a result, a progress report or a request diagnostic - as well as the
    /// <see cref="RequestDiagnostic.StartedAt"/> stamp on each reported request. The system clock is
    /// used when omitted. Waiting is not measurement and stays on the system clock regardless: the
    /// screening ceiling, the per-request HTTP timeouts and the pause between latency probes, the
    /// last of which <paramref name="delayProviderOverride"/> governs.
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

    /// <summary>
    /// The under-load probes go to the install's ping endpoint, on the address the transfer is
    /// actually using.
    /// </summary>
    /// <remarks>
    /// An upload is sent to the ping endpoint itself, once any redirect in front of it has been
    /// resolved, so that resolved address is probed as it stands: joining the ping path onto it as
    /// onto an install address would probe a file that does not exist. A download's transfer
    /// address is the install's own, which the ping path is joined onto as for the idle probe.
    /// </remarks>
    private protected override string LoadedLatencyProbeUrl(IServer server, string transferUrl) =>
        new Uri(transferUrl, UriKind.Absolute).AbsolutePath.EndsWith("/" + PingPath, StringComparison.Ordinal)
            ? transferUrl
            : LatencyProbeUrl(transferUrl);

    /// <summary>
    /// Whether the ping endpoint answered as LibreSpeed: with nothing at all.
    /// </summary>
    /// <remarks>
    /// <c>backend/empty.php</c> returns a success status and an empty body. Its content type is
    /// <c>text/html</c>, so that says nothing either way, and the body is the only part of the
    /// answer that distinguishes a LibreSpeed install from a plain web server serving a page at the
    /// same address. A non-success status, which is what an Ookla server gives this request, has
    /// already failed the request before it reaches here; see
    /// <see cref="LatencyStatusFailureMessage"/>.
    /// </remarks>
    private protected override bool IsLatencyResponseValid(string response) => response.Length == 0;

    /// <summary>
    /// A client error from the ping endpoint is reported as the wrong server, and any other
    /// non-success status as a server that failed. Both name the address.
    /// </summary>
    /// <remarks>
    /// The user names a LibreSpeed server, so a status such as 404 almost always means the address
    /// is wrong: naming the web root of an install under a path, or an Ookla server, answers
    /// exactly that. It is the same mistake as an address answering with a page, so it gets the
    /// same guidance. A server error such as 503 says the opposite: something at the right address
    /// is failing, often a proxy whose backend is down, so the user is told the status rather than
    /// to recheck an address that is correct.
    /// </remarks>
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

    /// <summary>
    /// The install's ping endpoint, which also accepts the upload POSTs.
    /// </summary>
    /// <remarks>
    /// LibreSpeed accepts a streamed body at <c>backend/empty.php</c> on the same address it answers
    /// the ping on. A redirect in front of the install, such as a reverse proxy sending plain HTTP
    /// to HTTPS, is resolved before the uploads begin, as it is for every provider.
    /// </remarks>
    private protected override string UploadUrl(string serverUrl) => InstallDirectory(serverUrl) + PingPath;

    /// <summary>
    /// The directory a LibreSpeed install is served from, as a prefix the endpoint paths are joined
    /// onto: the given address with its query and fragment dropped, a page name the browser showed
    /// dropped, and a trailing slash assured.
    /// </summary>
    /// <remarks>
    /// The rest of the given path is kept, which is what makes an install under a path work.
    /// Resolving <c>"."</c> against the address instead - what the Ookla provider does, where the
    /// address always names a file inside the install - would turn <c>http://nas.local/speedtest</c>
    /// into <c>http://nas.local/</c> and test the web root. So only a last segment that is plainly
    /// a page (<c>index.html</c>, copied from the browser's address bar) is dropped, and a trailing
    /// slash is added where the user left it off, so every form of the address tests the same
    /// server.
    /// </remarks>
    /// <example>
    /// Input:  "http://nas.local/speedtest"
    /// Output: "http://nas.local/speedtest/"
    /// Input:  "http://pi.local:8080/index.html?lang=en"
    /// Output: "http://pi.local:8080/"
    /// </example>
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
