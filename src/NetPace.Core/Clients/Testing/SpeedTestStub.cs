using NetPace.Core.Clients.Ookla;

namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A stub implementation of <see cref="ISpeedTestService"/> that reports the same fixed figures
/// every run, for testing purposes.
/// </summary>
/// <remarks>
/// <para>
/// The stub holds three servers and recognises one by its URL; the trailing digit of the matched
/// server's sponsor is the number its figures are built from. Anything whose URL the stub does not
/// hold, including a server named by an unmatched URL, counts as number 10. The number scales only
/// the elapsed time — the bytes and the request counts are the same for every server — so a
/// higher-numbered server is slower, matching its higher latency.
/// </para>
/// <list type="bullet">
/// <item><description>Latency is 100ms times the server number.</description></item>
/// <item><description>Jitter is 10ms times the server number — so 30ms for server 3 — beside that latency.</description></item>
/// <item><description>A download is always 1,000 bytes, taking 1 second times the server number — so 3 seconds for server 3 — from 150 requests, none failed.</description></item>
/// <item><description>An upload is always 7,000 bytes, taking 3 seconds times the server number, from 32 requests, none failed.</description></item>
/// <item><description>Screening wins the lowest-numbered candidate offered, at that server's latency and jitter.</description></item>
/// </list>
/// <para>
/// So the figures are fixed but not uniform: a consumer comparing two servers sees two different
/// speeds, and one measuring the same server twice sees the same speed.
/// </para>
/// <para>
/// A latency probe reports progress at 25, 50, 75 and 100 percent, screening at 33, 66 and 100,
/// and a transfer reports <see cref="SpeedTestProgressClimb"/> towards the result it goes on to
/// return. Constructed with a delay, the stub pauses by that many milliseconds before each report,
/// so a stubbed measurement unfolds rather than completing at once. The pause blocks the calling
/// thread rather than yielding, so the reports are observable only off that thread. A null progress
/// receiver is treated as none, as the real provider treats it.
/// </para>
/// <para>
/// The diagnostics overloads report nothing: the stub fabricates aggregate results rather than issuing requests, so there are no per-request diagnostics to report.
/// </para>
/// </remarks>
public sealed class SpeedTestStub : ISpeedTestService
{
    /// <summary>
    /// The latency a server reports per unit of its server number, so a higher-numbered server is
    /// slower.
    /// </summary>
    private const int LatencyMillisecondsPerServerNumber = 100;

    /// <summary>
    /// The jitter a server reports per unit of its server number, so a higher-numbered server is
    /// both slower and less steady.
    /// </summary>
    private const int JitterMillisecondsPerServerNumber = 10;

    private readonly IServer[] stubServers = new IServer[]
    {
        new Server { Location = "Location 1", Sponsor = "Test Sponsor 1", Url = "http://test1.com" },
        new Server { Location = "Location 2", Sponsor = "Test Sponsor 2", Url = "http://test2.com" },
        new Server { Location = "Location 3", Sponsor = "Test Sponsor 3", Url = "http://test3.com" },
    };

    private readonly int delayMilliseconds = 0;

    /// <summary>
    /// Constructs a new <see cref="SpeedTestStub"/> instance.
    /// </summary>
    public SpeedTestStub() { }

    /// <summary>
    /// Constructs a new <see cref="SpeedTestStub"/> instance that pauses by
    /// <paramref name="delayMilliseconds"/> before each progress report.
    /// </summary>
    /// <param name="delayMilliseconds">The pause before each progress report, in milliseconds.</param>
    public SpeedTestStub(int delayMilliseconds)
    {
        this.delayMilliseconds = delayMilliseconds;
    }

    private int GetServerID(string serverUrl)
    {
        // First see if we can match the server on our 'pre-canned list'
        var matched = stubServers.FirstOrDefault(s => s.Url.Equals(serverUrl));

        return matched != null
            ? int.Parse(matched.Sponsor!.Replace("Test Sponsor ", ""))
            : 10;
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(stubServers);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(server, new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        progress ??= new NullProgress<LatencyTestProgress>();

        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 25 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 50 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 75 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new LatencyTestProgress { PercentageComplete = 100 });

        var serverID = GetServerID(server.Url);

        var latencyResult = new LatencyTestResult
        {
            Server = server,
            LatencyMilliseconds = serverID * LatencyMillisecondsPerServerNumber,
            JitterMilliseconds = serverID * JitterMillisecondsPerServerNumber
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(serverUrl, new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        // Matches what OoklaSpeedtest builds for a server named by URL: a sponsor of "(Unknown)"
        // and no location, so a snapshot of this path records what the real client would report.
        var server = new Server() { Sponsor = "(Unknown)", Url = serverUrl };

        return GetServerLatencyAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(servers, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        progress ??= new NullProgress<SpeedTestProgress>();

        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new SpeedTestProgress { PercentageComplete = 33 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new SpeedTestProgress { PercentageComplete = 66 });
        Task.Delay(delayMilliseconds).Wait();
        progress.Report(new SpeedTestProgress { PercentageComplete = 100 });

        // Every candidate offered is screened and the lowest-numbered one wins. Callers screen a
        // single candidate through this method as well as a list, so the winner is taken from the
        // array given rather than from the stub's own server list.
        var fastest = servers.OrderBy(candidate => GetServerID(candidate.Url)).First();

        var serverID = GetServerID(fastest.Url);

        var latencyResult = new LatencyTestResult
        {
            Server = fastest,
            LatencyMilliseconds = serverID * LatencyMillisecondsPerServerNumber,
            JitterMilliseconds = serverID * JitterMillisecondsPerServerNumber
        };

        return Task.FromResult(latencyResult);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        var serverID = GetServerID(server.Url);

        var result = new SpeedTestResult() { BytesProcessed = 1000, ElapsedMilliseconds = 1000 * serverID, RequestsSucceeded = 150, RequestsFailed = 0 };

        ReportClimbTo(result, progress ?? new NullProgress<SpeedTestProgress>());

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, CancellationToken cancellationToken = default)
    {
        var serverID = GetServerID(server.Url);

        var result = new SpeedTestResult() { BytesProcessed = 7000, ElapsedMilliseconds = 3000 * serverID, RequestsSucceeded = 32, RequestsFailed = 0 };

        ReportClimbTo(result, progress ?? new NullProgress<SpeedTestProgress>());

        return Task.FromResult(result);
    }

    /// <summary>
    /// Reports progress towards <paramref name="result"/>, pausing by the configured delay before
    /// each report.
    /// </summary>
    private void ReportClimbTo(SpeedTestResult result, IProgress<SpeedTestProgress> progress)
    {
        foreach (SpeedTestProgress report in SpeedTestProgressClimb.Towards(result))
        {
            Task.Delay(delayMilliseconds).Wait();

            progress.Report(report);
        }
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(server, progress ?? new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetServerLatencyAsync(serverUrl, progress ?? new NullProgress<LatencyTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(servers, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetDownloadSpeedAsync(server, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return GetUploadSpeedAsync(server, progress ?? new NullProgress<SpeedTestProgress>(), cancellationToken);
    }
}
