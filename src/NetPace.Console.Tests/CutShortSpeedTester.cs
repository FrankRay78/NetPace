using NetPace.Core;

namespace NetPace.Console.Tests;

/// <summary>
/// Where in a run <see cref="CutShortSpeedTester"/> reaches the run's time limit.
/// </summary>
public enum CutShortStage
{
    /// <summary>While a server is still being looked for, so the run never settles on one.</summary>
    ChoosingServer,

    /// <summary>During the latency test, before any probe has answered.</summary>
    Latency,

    /// <summary>During the download test, once it has delivered something.</summary>
    Download,
}

/// <summary>
/// A speed tester standing in for a server slow enough to outlast the run's time limit: the
/// download test reaches the limit as it runs, and answers as a measurement cut short at one
/// does - the figure of what it delivered before it stopped, no failed request, and one request
/// recorded as stopped by the caller.
/// </summary>
/// <remarks>
/// <para>
/// The stop-and-report behaviour itself belongs to <c>NetPace.Core</c> and is verified there. This
/// double exists so that a CLI test can watch what the run does about a measurement that was cut
/// short: which tests it goes on to run, what it writes, what it exits with, and how it words the
/// record of a stopped request.
/// </para>
/// <para>
/// An upload test answers cleanly, so a test can tell an upload that was never run from one that
/// was. Which runs are cut short is <see cref="CutsShortRun"/>'s to say, so a repeated run can
/// have its first run cut short and its second complete.
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
    private LatencyTestResult MeasureLatency(IServer server)
    {
        if (ReachLimitAt(CutShortStage.Latency))
        {
            throw new OperationCanceledException();
        }

        return Latency(server);
    }

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default) =>
        Task.FromResult(MeasureLatency(server));

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(Latency(new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }));

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default) =>
        Task.FromResult(MeasureLatency(new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }));

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default) =>
        Task.FromResult(Latency(servers[0]));

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, cancellationToken);

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, null, null, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, progress, null, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        // The limit is reached while the download is still running, which is what a slow server
        // does to a run. The request that was in flight is recorded the way the library records
        // one: cancelled, carrying its own generic reason for a stop.
        if (!ReachLimitAt(CutShortStage.Download))
        {
            Report(diagnostics, sequence: 1, RequestOutcome.Succeeded, failureReason: null);
            return Task.FromResult(Clean(bytes: 4_000_000));
        }

        Report(diagnostics, sequence: 1, RequestOutcome.Succeeded, failureReason: null);
        Report(diagnostics, sequence: 2, RequestOutcome.Cancelled, RequestDiagnostic.StoppedByCaller);

        return Task.FromResult(Clean(bytes: 1_000_000));
    }

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, null, null, cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, progress, null, cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default, CancellationToken stopAndReportToken = default)
    {
        UploadCalls++;

        Report(diagnostics, sequence: 1, RequestOutcome.Succeeded, failureReason: null);
        return Task.FromResult(Clean(bytes: 500_000));
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

    /// <summary>A measurement whose every counted request succeeded, over one second.</summary>
    private static SpeedTestResult Clean(long bytes) =>
        new() { BytesProcessed = bytes, ElapsedMilliseconds = 1000, RequestsSucceeded = 1, RequestsFailed = 0, Latency = null };

    /// <summary>A fixed latency figure, with no run of probe times behind it.</summary>
    private static LatencyTestResult Latency(IServer server) =>
        new() { Server = server, LatencyMilliseconds = 24, MinimumLatencyMilliseconds = null, MaximumLatencyMilliseconds = null, JitterMilliseconds = 3 };

    private static void Report(IProgress<RequestDiagnostic>? diagnostics, int sequence, RequestOutcome outcome, string? failureReason) =>
        diagnostics?.Report(new RequestDiagnostic
        {
            StartedAt = new DateTime(1980, 1, 1, 10, 5, 0, DateTimeKind.Unspecified),
            Kind = RequestKind.Transfer,
            Sequence = sequence,
            Url = Url,
            Outcome = outcome,
            BytesProcessed = outcome == RequestOutcome.Succeeded ? 1_000_000 : 0,
            ElapsedMilliseconds = 1000,
            FailureReason = failureReason
        });
}
