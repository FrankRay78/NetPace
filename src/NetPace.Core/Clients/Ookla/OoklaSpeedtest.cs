using System.Net;
using NetPace.Core.Clients.Ookla.Extensions;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
/// <remarks>
/// The measurement itself lives in <see cref="SpeedTestProviderBase"/>, shared with every other
/// provider. What is here is what an Ookla server needs that another kind does not: the
/// <c>latency.txt</c> probe and the answer it must give, the <c>random{N}x{N}.jpg</c> download
/// requests, the redirect probe that finds where uploads are accepted, and the server feed.
/// </remarks>
public sealed class OoklaSpeedtest : SpeedTestProviderBase
{
    private const string LatencyFileName = "latency.txt";
    private const string LatencyResponsePrefix = "test=test";

    private readonly OoklaSpeedtestSettings settings;

    /// <summary>
    /// Constructs a new instance of the <see cref="OoklaSpeedtest"/> class.
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
    /// alone: the base implementations of those two read the system stopwatch, so a provider that
    /// overrides only <see cref="TimeProvider.GetUtcNow"/> yields controlled stamps beside
    /// wall-clock durations, with nothing reporting the mismatch. It must also be monotonic, as
    /// <see cref="TimeProvider.GetTimestamp"/> requires; a reading that goes backwards is rejected
    /// rather than reported as a negative speed. Requests that overlap share whatever clock is
    /// supplied, so a caller reading a per-request figure off a clock it advances itself needs one
    /// request outstanding at a time.
    /// </para>
    /// </param>
    public OoklaSpeedtest(OoklaSpeedtestSettings? speedtestSettings = null, HttpClient? httpClientOverride = null, IDelayProvider? delayProviderOverride = null, TimeProvider? timeProviderOverride = null)
        : base(speedtestSettings ?? new OoklaSpeedtestSettings(), httpClientOverride, delayProviderOverride, timeProviderOverride)
    {
        settings = (OoklaSpeedtestSettings)SharedSettings;
    }

    /// <inheritdoc/>
    public override async Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        var serversXml = await Client.GetStringAsync(settings.ServersUrl, cancellationToken).ConfigureAwait(false);
        var servers = serversXml.DeserializeFromXml()?.Servers ?? Array.Empty<OoklaServer>();
        return servers.Where(s =>
                !string.IsNullOrWhiteSpace(s.Location) &&
                !string.IsNullOrWhiteSpace(s.Sponsor) &&
                !string.IsNullOrWhiteSpace(s.Url)).ToArray();
    }

    /// <inheritdoc/>
    private protected override string LatencyProbeUrl(string serverUrl) => GetBaseUrl(serverUrl) + LatencyFileName;

    /// <inheritdoc/>
    private protected override bool IsLatencyResponseValid(string response) => response.StartsWith(LatencyResponsePrefix);

    /// <inheritdoc/>
    private protected override string LatencyResponseRejectedMessage(string probeUrl) =>
        $"Server returned incorrect test string for {LatencyFileName}";

    /// <inheritdoc/>
    /// <example>
    /// http://manchester.speedtest.boundlessnetworks.uk:8080/speedtest/random1500x1500.jpg?r=0
    /// http://manchester.speedtest.boundlessnetworks.uk:8080/speedtest/random1500x1500.jpg?r=1
    /// ...
    /// </example>
    private protected override IEnumerable<string> DownloadRequestUrls(string serverUrl)
    {
        var downloadUrl = GetBaseUrl(serverUrl) + "random{0}x{0}.jpg?r={1}";

        foreach (var downloadSize in settings.DownloadSizes)
        {
            for (var i = 0; i < settings.DownloadSizeIterations; i++)
            {
                yield return string.Format(downloadUrl, downloadSize, i);
            }
        }
    }

    /// <summary>
    /// Resolves the address the server actually wants uploads sent to, following any redirects
    /// with an empty probe POST before the measured uploads begin.
    /// </summary>
    /// <remarks>
    /// Ookla is migrating its fleet to HTTPS, and a migrated server answers a plain-HTTP upload
    /// POST with a redirect. The streaming body cannot survive that redirect - the server responds
    /// and closes the request stream while the body is still being written - so the real endpoint is
    /// resolved once, cheaply, and the uploads go straight to it.
    /// <para>
    /// The probe carries no body: a redirect is decided by scheme and host rather than payload, so
    /// an empty POST draws the same answer with nothing to write and nothing to tear down mid-write.
    /// Sending no bytes also keeps the probe out of the measurement entirely. Whether the redirect is
    /// followed by the underlying handler or reported back to us, the endpoint that answered the probe
    /// is the one returned - whether or not it answered with success. A rejection is recorded so a
    /// triager can see it, not acted on. The probe never fails the test, other than on caller
    /// cancellation, which propagates: if it cannot complete at all, the original URL is used and
    /// any real fault surfaces through the uploads themselves.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The address to upload to, and how many requests the probe issued getting there - the
    /// measured uploads are numbered after those. The under-load latency probes are numbered
    /// separately, so a sequence number is distinct only within one kind; see
    /// <see cref="RequestDiagnostic.Sequence"/>.
    /// </returns>
    private protected override async Task<(string Url, int RequestsIssued)> ResolveUploadUrlAsync(string serverUrl, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken)
    {
        const int maximumHops = 5;

        var currentUrl = serverUrl;
        var requestsIssued = 0;

        for (var hop = 0; hop < maximumHops; hop++)
        {
            var probeUrl = currentUrl;
            var (startedAt, startTimestamp) = StartRequest();
            requestsIssued++;

            try
            {
                using var probeContent = new RandomStreamContent(0);
                using var response = await Client.PostAsync(currentUrl, probeContent, cancellationToken).ConfigureAwait(false);

                // PostAsync does not throw on a non-success status and the probe never calls
                // EnsureSuccessStatusCode, so without this the one request that can quietly
                // misdirect every upload that follows is the one record that always reads ok.
                var redirected = IsRedirect(response.StatusCode);
                var probeAnswered = response.IsSuccessStatusCode || redirected;

                if (diagnostics is not null)
                {
                    ReportProgress(diagnostics, new RequestDiagnostic
                    {
                        StartedAt = startedAt,
                        Kind = RequestKind.EndpointProbe,
                        Sequence = requestsIssued,
                        Url = probeUrl,
                        Outcome = probeAnswered ? RequestOutcome.Succeeded : RequestOutcome.Failed,
                        BytesProcessed = 0,
                        ElapsedMilliseconds = ElapsedMillisecondsSince(startTimestamp),
                        FailureReason = probeAnswered ? null : $"{EndpointProbeAnswered} {(int)response.StatusCode}"
                    });
                }

                // The handler did not follow the redirect for us - follow it explicitly. A
                // Location that will not compose into an address is not a transport failure, so it
                // ends the hops here rather than being recorded as a failed request.
                if (redirected && response.Headers.Location is { } location)
                {
                    if (Uri.TryCreate(new Uri(currentUrl), location, out var redirectTarget))
                    {
                        currentUrl = redirectTarget.ToString();
                        continue;
                    }

                    return (currentUrl, requestsIssued);
                }

                // Otherwise the endpoint that answered is the one to upload to. When the handler
                // followed redirects itself, that is the final hop rather than where we started.
                return (response.RequestMessage?.RequestUri?.ToString() ?? currentUrl, requestsIssued);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException)
            {
                ReportRequestFailure(diagnostics, RequestKind.EndpointProbe, startedAt, requestsIssued, probeUrl, ElapsedMillisecondsSince(startTimestamp), e, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                // Probing is best-effort; fall back to the configured URL.
                return (serverUrl, requestsIssued);
            }
        }

        return (currentUrl, requestsIssued);
    }

    /// <summary>
    /// Returns the base URL (ending with a trailing slash) by removing
    /// the file name and query parameters from a full URL string.
    /// </summary>
    /// <example>
    /// Input:  "http://example.com/path/speedtest/file.jpg?x=1"
    /// Output: "http://example.com/path/speedtest/"
    /// </example>
    private static string GetBaseUrl(string url)
    {
        var uri = new Uri(url);
        var baseUri = new Uri(uri, ".");
        return baseUri.ToString();
    }

    /// <summary>
    /// Determines whether a status code asks the caller to repeat the request elsewhere.
    /// </summary>
    /// <remarks>
    /// The permanent and temporary redirects are treated alike because this resolves an address
    /// rather than replaying a transfer: an HTTPS migration is as likely to be announced with a
    /// permanent 301 as with the 307 that prompted this, and either way the answer we want is
    /// simply "go here instead". The method rewriting that a handler applies to 301/302/303 when
    /// replaying a request does not apply - the probe is discarded once its address is known.
    /// </remarks>
    private static bool IsRedirect(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.MovedPermanently or
        HttpStatusCode.Found or
        HttpStatusCode.SeeOther or
        HttpStatusCode.TemporaryRedirect or
        HttpStatusCode.PermanentRedirect;
}
