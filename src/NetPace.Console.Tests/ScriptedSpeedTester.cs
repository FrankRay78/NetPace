using NetPace.Core;
using NetPace.Core.Clients.Ookla;

namespace NetPace.Console.Tests;

/// <summary>
/// A configurable <see cref="ISpeedTestService"/> for CLI failure scenarios: it always finds a
/// server (Deutsche Telekom-style URL, matching the issue #206 example), and returns download/upload
/// results supplied per call index so a test can script clean, partial, and all-failed iterations.
/// Optionally streams a per-request failure reason on the progress channel for debug-verbosity tests.
/// </summary>
public sealed class ScriptedSpeedTester : ISpeedTestService
{
    public const string Url = "http://ffm.wsqm.telekom-dienste.de:8080/speedtest/upload.php";

    private readonly IServer server = new Server { Location = "Frankfurt", Sponsor = "Deutsche Telekom", Url = Url };

    private int downloadCall;
    private int uploadCall;

    /// <summary>Supplies the download result for a given zero-based call index.</summary>
    public Func<int, SpeedTestResult> DownloadFactory { get; set; } = _ => Clean(150);

    /// <summary>Supplies the upload result for a given zero-based call index.</summary>
    public Func<int, SpeedTestResult> UploadFactory { get; set; } = _ => Clean(32);

    /// <summary>When set, a request failure reason streamed on the progress channel.</summary>
    public string? StreamedFailureReason { get; set; }

    /// <summary>A clean result: every request succeeded.</summary>
    public static SpeedTestResult Clean(int attempts) =>
        new() { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsAttempted = attempts, RequestsSucceeded = attempts, RequestsFailed = 0 };

    /// <summary>An all-failed result: zero bytes, every request failed.</summary>
    public static SpeedTestResult AllFailed(int attempts) =>
        new() { BytesProcessed = 0, ElapsedMilliseconds = 1000, RequestsAttempted = attempts, RequestsSucceeded = 0, RequestsFailed = attempts };

    /// <summary>A partial result: some requests failed, the rest contributed bytes.</summary>
    public static SpeedTestResult Partial(int attempts, int failed) =>
        new() { BytesProcessed = 1000, ElapsedMilliseconds = 1000, RequestsAttempted = attempts, RequestsSucceeded = attempts - failed, RequestsFailed = failed };

    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new[] { server });

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

    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var result = DownloadFactory(downloadCall++);
        StreamReasonIfFailed(result, progress);
        return Task.FromResult(result);
    }

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default) =>
        GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);

    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var result = UploadFactory(uploadCall++);
        StreamReasonIfFailed(result, progress);
        return Task.FromResult(result);
    }

    private void StreamReasonIfFailed(SpeedTestResult result, IProgress<SpeedTestProgress> progress)
    {
        if (StreamedFailureReason is not null && result.RequestsFailed > 0)
        {
            progress.Report(new SpeedTestProgress { PercentageComplete = 100, FailedRequestReason = StreamedFailureReason });
        }
    }
}
