using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// Where in a run <see cref="CutShortSpeedTester"/> reaches the run's time limit.
/// </summary>
public enum CutShortStage
{
    /// <summary>While a server is still being looked for, so the run never settles on one.</summary>
    ChoosingServer,

    /// <summary>
    /// While the candidates are being screened, with one probe still out, so the run never
    /// settles on one.
    /// </summary>
    Screening,

    /// <summary>During the latency test, before any probe has answered.</summary>
    Latency,

    /// <summary>During the download test, once it has delivered something.</summary>
    Download,

    /// <summary>During the upload test, once it has delivered something.</summary>
    Upload,
}

/// <summary>
/// A speed tester standing in for a server slow enough to outlast the run's time limit: the
/// transfer test the limit falls in reaches it as it runs, and answers as a measurement cut short
/// at one does - the figure of what it delivered before it stopped, as many failed requests as
/// <see cref="CutShortFailures"/> says (none by default), and one request recorded as stopped by
/// the caller. The failed requests are counted only: no diagnostic record is written for them, so
/// a <c>--diagnostics</c> test that sets <see cref="CutShortFailures"/> would see records that do
/// not add up to the counts.
/// </summary>
/// <remarks>
/// <para>
/// Stop-and-report itself belongs to <c>NetPace.Core</c>. This double lets a CLI test watch what
/// the run does about a cut-short measurement.
/// </para>
/// <para>
/// A transfer the limit misses answers cleanly, so a test can tell an upload never run from one
/// that was; <see cref="CutsShortRun"/> says which runs are cut short.
/// </para>
/// </remarks>
public sealed class CutShortSpeedTester(RecordingRunTimeLimit limit) : ISpeedTestService
{
    public const string Url = "http://cutshort.example.com/speedtest/upload.php";

    private int runs;
    private int currentRun;

    /// <summary>The server every route reports, named so a snapshot reads the same way each run.</summary>
    public IServer Server { get; } = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = Url };

    /// <summary>
    /// Whether the run of the given zero-based index reaches the limit at all. Every run does by
    /// default.
    /// </summary>
    public Func<int, bool> CutsShortRun { get; set; } = _ => true;

    /// <summary>Where in a cut-short run the limit is reached.</summary>
    public CutShortStage Stage { get; set; } = CutShortStage.Download;

    /// <summary>What the transfer cut short had delivered when the limit was reached.</summary>
    public long CutShortBytes { get; set; } = 1_000_000;

    /// <summary>How many of the cut-short transfer's requests had failed when the limit was reached.</summary>
    public int CutShortFailures { get; set; }

    /// <summary>How many times the upload test was asked for, so a test can assert it was not.</summary>
    public int UploadCalls { get; private set; }

    /// <summary>
    /// Counted here, so every stage below reads the same run index: a run always looks for a
    /// server before it measures anything.
    /// </summary>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        currentRun = runs++;

        ReachLimitAt(CutShortStage.ChoosingServer);

        // The run's limit is linked into the token discovery was given, so a search still going
        // when the limit is reached ends the way a cancelled one does.
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IServer[]>([Server]);
    }

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default) =>
        Task.FromResult(Latency(server));

    /// <summary>
    /// The latency measurement, reached by every overload: it answers with a figure unless the
    /// limit is reached here, where no probe has answered and so there is nothing to report.
    /// </summary>
    private LatencyTestResult MeasureLatency(IServer server, CancellationToken stopAndReportToken)
    {
        if (ReachLimitAt(CutShortStage.Latency))
        {
            RequireStop(stopAndReportToken);
            throw new OperationCanceledException(stopAndReportToken);
        }

        return Latency(server);
    }

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default) =>
        Task.FromResult(MeasureLatency(server, stopAndReportToken));

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Latency(new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }));

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default) =>
        Task.FromResult(MeasureLatency(new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }, stopAndReportToken));

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default) =>
        Task.FromResult(Latency(servers[0]));

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, cancellationToken);

    /// <summary>
    /// Screening, which takes no stop of its own: the run's limit is linked into its token, so a
    /// probe the limit cuts off is recorded as the library records one its caller cancelled.
    /// </summary>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        if (ReachLimitAt(CutShortStage.Screening))
        {
            Report(diagnostics, sequence: 1, RequestOutcome.Cancelled, "cancelled by caller", RequestKind.LatencyProbe);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return GetFastestServerByLatencyAsync(servers, cancellationToken);
    }

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, null, null, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, progress, null, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        return Task.FromResult(Transfer(CutShortStage.Download, diagnostics, stopAndReportToken, wholeBytes: 4_000_000));
    }

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, null, null, cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, progress, null, cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        UploadCalls++;

        return Task.FromResult(Transfer(CutShortStage.Upload, diagnostics, stopAndReportToken, wholeBytes: 500_000));
    }

    /// <summary>
    /// A transfer that answers cleanly unless <paramref name="stage"/> is where the run reaches its
    /// limit. There the limit is reached while the transfer is still running, which is what a slow
    /// server does to a run, and the request that was in flight is recorded the way the library
    /// records one: cancelled, carrying its own generic reason for a stop.
    /// </summary>
    private SpeedTestResult Transfer(CutShortStage stage, IProgress<RequestDiagnostic>? diagnostics, CancellationToken stopAndReportToken, long wholeBytes)
    {
        if (!ReachLimitAt(stage))
        {
            Report(diagnostics, sequence: 1, RequestOutcome.Succeeded, failureReason: null);
            return Clean(wholeBytes);
        }

        RequireStop(stopAndReportToken);

        // A transfer that delivered nothing had no request succeed, so one whose only requests
        // failed reads as the all-failed transfer it is.
        var succeeded = CutShortBytes > 0 ? 1 : 0;

        if (succeeded > 0)
        {
            Report(diagnostics, sequence: 1, RequestOutcome.Succeeded, failureReason: null);
        }

        Report(diagnostics, sequence: 2, RequestOutcome.Cancelled, RequestDiagnostic.StoppedByCaller);

        return Clean(CutShortBytes) with { RequestsSucceeded = succeeded, RequestsFailed = CutShortFailures };
    }

    /// <summary>
    /// Reaches the run's limit where <paramref name="stage"/> is where this run is cut short, and
    /// says whether it did.
    /// </summary>
    private bool ReachLimitAt(CutShortStage stage)
    {
        if (Stage != stage || !CutsShortRun(currentRun))
        {
            return false;
        }

        limit.Reach();
        return true;
    }

    /// <summary>
    /// Fails the test unless the measurement was handed the run's limit as its stop: the stop is
    /// what makes the library hand back a figure, so a run that never passed it on would, against
    /// a real server, run past the limit.
    /// </summary>
    private static void RequireStop(CancellationToken stopAndReportToken)
    {
        if (!stopAndReportToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("The run's time limit was reached, but the measurement was not given it as its stop.");
        }
    }

    /// <summary>A measurement whose every counted request succeeded, over one second.</summary>
    private static SpeedTestResult Clean(long bytes) =>
        new() { BytesProcessed = bytes, ElapsedMilliseconds = 1000, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null };

    /// <summary>A fixed latency figure, with no run of probe times behind it.</summary>
    private static LatencyTestResult Latency(IServer server) =>
        new() { Server = server, LatencyMilliseconds = 24, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 3 };

    private static void Report(IProgress<RequestDiagnostic>? diagnostics, int sequence, RequestOutcome outcome, string? failureReason, RequestKind kind = RequestKind.Transfer) =>
        diagnostics?.Report(new RequestDiagnostic
        {
            StartedAt = new DateTime(1980, 1, 1, 10, 5, 0, DateTimeKind.Unspecified),
            Kind = kind,
            Sequence = sequence,
            Url = Url,
            Outcome = outcome,
            BytesProcessed = outcome == RequestOutcome.Succeeded ? 1_000_000 : 0,
            ElapsedMilliseconds = 1000,
            FailureReason = failureReason
        });
}
