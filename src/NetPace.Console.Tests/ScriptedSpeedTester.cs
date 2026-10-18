using NetPace.Core;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

/// <summary>
/// A configurable <see cref="ISpeedTestService"/> for CLI failure scenarios: it always finds a
/// server (Deutsche Telekom-style URL), and returns download/upload
/// results supplied per call index so a test can script clean, partial, and all-failed iterations.
/// </summary>
public sealed class ScriptedSpeedTester : ISpeedTestService
{
    public const string Url = "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/upload.php";

    private int downloadCall;
    private int uploadCall;

    /// <summary>
    /// The server <see cref="GetServersAsync"/> returns. The by-URL latency overloads build their
    /// own server from the URL they are given and do not read this. Settable so a test can supply a
    /// sponsor, location or URL long enough to cross a narrow destination's width.
    /// </summary>
    public IServer Server { get; set; } = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = Url };

    /// <summary>
    /// Whether a measurement reports progress on its way to the result. Off by default: the
    /// failure scenarios this tester was written for care only about the result, and a measurement
    /// that reports nothing is the quietest stand-in for one. A test that watches the live display
    /// turns it on.
    /// </summary>
    public bool ReportsProgress { get; set; }

    /// <summary>Supplies the download result for a given zero-based call index.</summary>
    public Func<int, SpeedTestResult> DownloadFactory { get; set; } = _ => Clean(150);

    /// <summary>Supplies the upload result for a given zero-based call index.</summary>
    public Func<int, SpeedTestResult> UploadFactory { get; set; } = _ => Clean(32);

    /// <summary>A clean result: every request succeeded.</summary>
    public static SpeedTestResult Clean(int attempts) =>
        new() { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsSucceeded = attempts, RequestsFailed = 0 };

    /// <summary>An all-failed result: zero bytes, every request failed.</summary>
    public static SpeedTestResult AllFailed(int attempts) =>
        new() { BytesProcessed = 0, ElapsedMilliseconds = 1000, RequestsSucceeded = 0, RequestsFailed = attempts };

    /// <summary>A partial result: some requests failed, the rest contributed bytes.</summary>
    public static SpeedTestResult Partial(int attempts, int failed) =>
        new() { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsSucceeded = attempts - failed, RequestsFailed = failed };

    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new[] { Server });

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LatencyTestResult { Server = server, LatencyMilliseconds = 24 });

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LatencyTestResult { Server = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = serverUrl }, LatencyMilliseconds = 24 });

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, cancellationToken);

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LatencyTestResult { Server = servers[0], LatencyMilliseconds = 24 });

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        Task.FromResult(Measure(DownloadFactory(downloadCall++), progress));

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default) =>
        Task.FromResult(Measure(UploadFactory(uploadCall++), progress));

    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(server, cancellationToken);

    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        GetServerLatencyAsync(serverUrl, cancellationToken);

    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        GetFastestServerByLatencyAsync(servers, cancellationToken);

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        Task.FromResult(Measure(DownloadFactory(downloadCall++), progress));

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default) =>
        Task.FromResult(Measure(UploadFactory(uploadCall++), progress));

    /// <summary>
    /// Reports progress towards <paramref name="result"/> in four updates and returns it, the last
    /// update being the result itself. Bytes trail the clock early on and catch it up, so a caller
    /// showing the average speed since the test began watches it climb and settle on exactly the
    /// speed the result reports.
    /// </summary>
    private SpeedTestResult Measure(SpeedTestResult result, IProgress<SpeedTestProgress>? progress)
    {
        if (progress is null || !ReportsProgress)
        {
            return result;
        }

        (int percentageComplete, double bytesSoFar)[] updates =
        [
            (25, 0.10),
            (50, 0.35),
            (75, 0.65),
            (100, 1.00),
        ];

        foreach (var (percentageComplete, bytesSoFar) in updates)
        {
            progress.Report(new SpeedTestProgress
            {
                PercentageComplete = percentageComplete,
                BytesProcessed = (long)(result.BytesProcessed * bytesSoFar),
                ElapsedMilliseconds = result.ElapsedMilliseconds * percentageComplete / 100
            });
        }

        return result;
    }
}
