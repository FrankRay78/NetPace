using NetPace.Core.Clients.Ookla.Extensions;

namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// An Ookla Speedtest implementation of the <see cref="ISpeedTestService"/> interface.
/// </summary>
/// <remarks>
/// The measurement itself lives in <see cref="SpeedTestProviderBase"/>; what is here is what an
/// Ookla server needs that another kind does not.
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
    /// The clock every <c>ElapsedMilliseconds</c> and <see cref="RequestDiagnostic.StartedAt"/> is
    /// read from; the system clock when omitted. Waiting is not measurement and stays on the system
    /// clock regardless: the screening ceiling, the per-request HTTP timeouts, and the pause between
    /// latency probes that <paramref name="delayProviderOverride"/> governs.
    /// <para>
    /// A clock supplied here must override <see cref="TimeProvider.GetTimestamp"/> and
    /// <see cref="TimeProvider.TimestampFrequency"/>, not <see cref="TimeProvider.GetUtcNow"/>
    /// alone, whose base implementations read the system stopwatch, and must be monotonic: a
    /// reading that goes backwards is rejected rather than reported as a negative speed. Overlapping
    /// requests share the clock, so a caller reading a per-request figure off a clock it advances
    /// itself needs one request outstanding at a time.
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

    /// <inheritdoc/>
    /// <remarks>
    /// An Ookla server lists its upload endpoint as its address, so that is used as it stands.
    /// </remarks>
    private protected override string UploadUrl(string serverUrl) => serverUrl;

    // "http://example.com/path/speedtest/file.jpg?x=1" -> "http://example.com/path/speedtest/"
    private static string GetBaseUrl(string url)
    {
        var uri = new Uri(url);
        var baseUri = new Uri(uri, ".");
        return baseUri.ToString();
    }
}
