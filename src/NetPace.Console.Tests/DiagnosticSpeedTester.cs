using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// An <see cref="ISpeedTestService"/> that reports per-request diagnostics, so the console's
/// diagnostic assembly can be snapshotted end to end.
/// </summary>
/// <remarks>
/// Deliberately tiny and self-consistent: a handful of requests per test, with results whose counts
/// and bytes are exactly what the reported records add up to. Each transfer also sends one latency
/// probe, so the records cover a request a test makes without measuring. <see cref="SpeedTestStub"/> fabricates
/// aggregate results with no requests behind them, which is fine for result snapshots but would make
/// a diagnostic snapshot disagree with itself.
/// </remarks>
public sealed class DiagnosticSpeedTester : ISpeedTestService
{
    /// <summary>The URL the tester's server answers on.</summary>
    public const string Url = "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/upload.php";

    private const string LatencyUrl = "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/latency.txt";

    /// <summary>The reason a failing upload reports, as a real TLS rejection would.</summary>
    public const string UploadFailureReason =
        "The SSL connection could not be established, see inner exception. <- Authentication failed because the remote party sent a TLS alert: 'HandshakeFailure'.";

    private static readonly DateTime RequestsStartAt = new DateTime(1980, 1, 1, 11, 0, 0);

    /// <summary>
    /// The latency a transfer reports having measured under its own load, from the one probe the
    /// tester sends while it runs - so a diagnostic snapshot carries a probe record whose figure the
    /// result agrees with.
    /// </summary>
    private static LatencyTestResult LoadedLatency(IServer server) => new()
    {
        Server = server,
        LatencyMilliseconds = 61,
        MinimumLatencyMilliseconds = 61,
        MaximumLatencyMilliseconds = 61,
        JitterMilliseconds = 0
    };

    private readonly IServer defaultServer = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = Url };

    private int requestsIssued = -1;

    /// <summary>
    /// Gets or initialises whether every upload request fails - the #245 shape, where a server
    /// rejects every upload and the result alone cannot say why.
    /// </summary>
    public bool FailEveryUpload { get; init; }

    /// <summary>
    /// Raised instead of choosing a server, standing in for an operational fault - NetPace's own
    /// health rather than a network condition - which propagates out of the command.
    /// </summary>
    public Exception? SelectionFault { get; init; }

    /// <inheritdoc />
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IServer[]>([defaultServer]);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, progress, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        for (var sequence = 1; sequence <= 2; sequence++)
        {
            Report(diagnostics, sequence, LatencyUrl, RequestOutcome.Succeeded, bytes: 9, durationMilliseconds: 24, kind: RequestKind.LatencyProbe);
        }

        return Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 24, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 0 });
    }

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, progress, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }, progress, diagnostics, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, progress, null, cancellationToken);

    /// <inheritdoc />
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        if (SelectionFault is not null)
        {
            throw SelectionFault;
        }

        Report(diagnostics, sequence: 1, LatencyUrl, RequestOutcome.Succeeded, bytes: 9, durationMilliseconds: 31, kind: RequestKind.LatencyProbe);

        return Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 31, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 0 });
    }

    /// <inheritdoc />
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, progress, null, cancellationToken);

    /// <inheritdoc />
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        for (var sequence = 1; sequence <= 2; sequence++)
        {
            Report(diagnostics, sequence, "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/random1500x1500.jpg?r=" + sequence, RequestOutcome.Succeeded, bytes: 500, durationMilliseconds: 250);
        }

        // One latency probe sent while the download ran. It is a request the test made but not one
        // it measured, so the summary must record it without counting it.
        Report(diagnostics, sequence: 1, LatencyUrl, RequestOutcome.Succeeded, bytes: 9, durationMilliseconds: 61, kind: RequestKind.LatencyProbe);

        return Task.FromResult(new SpeedTestResult { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsSucceeded = 2, RequestsFailed = 0, Latency = LoadedLatency(server) });
    }

    /// <inheritdoc />
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, null, null, cancellationToken);

    /// <inheritdoc />
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, progress, null, cancellationToken);

    /// <inheritdoc />
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        for (var sequence = 1; sequence <= 2; sequence++)
        {
            if (FailEveryUpload)
            {
                Report(diagnostics, sequence, Url, RequestOutcome.Failed, bytes: 0, durationMilliseconds: 1203, reason: UploadFailureReason, failureType: "HttpRequestException");
            }
            else
            {
                Report(diagnostics, sequence, Url, RequestOutcome.Succeeded, bytes: 3500, durationMilliseconds: 1500);
            }
        }

        Report(diagnostics, sequence: 1, LatencyUrl, RequestOutcome.Succeeded, bytes: 9, durationMilliseconds: 61, kind: RequestKind.LatencyProbe);

        // An upload whose every request failed moved nothing, so its probes saw no loaded line and
        // it reports no under-load latency - the probe record above is still the account of them.
        return FailEveryUpload
            ? Task.FromResult(new SpeedTestResult { BytesProcessed = 0, ElapsedMilliseconds = 3000, RequestsSucceeded = 0, RequestsFailed = 2, Latency = null })
            : Task.FromResult(new SpeedTestResult { BytesProcessed = 7000, ElapsedMilliseconds = 3000, RequestsSucceeded = 2, RequestsFailed = 0, Latency = LoadedLatency(server) });
    }

    /// <summary>
    /// Reports one request, stamped a second later than the last, so the order records were issued
    /// in is visible in a snapshot without depending on real time.
    /// </summary>
    private void Report(IProgress<RequestDiagnostic>? diagnostics, int sequence, string url, RequestOutcome outcome, long bytes, long durationMilliseconds, string? reason = null, string? failureType = null, RequestKind kind = RequestKind.Transfer)
    {
        requestsIssued++;

        diagnostics?.Report(new RequestDiagnostic
        {
            StartedAt = RequestsStartAt.AddSeconds(requestsIssued),
            Kind = kind,
            Sequence = sequence,
            Url = url,
            Outcome = outcome,
            BytesProcessed = bytes,
            ElapsedMilliseconds = durationMilliseconds,
            FailureReason = reason,
            FailureType = failureType
        });
    }
}
