namespace NetPace.Core.Clients.Testing;

/// <summary>
/// An implementation of <see cref="ISpeedTestService"/> that
/// simulates fluctuating speeds across successive network speed tests.
/// </summary>
/// <remarks>
/// <para>
/// The tester offers one server, and screening always wins the first candidate it is given,
/// whatever list that is, so the figures are the only thing that varies. A new test begins each
/// time a caller screens the servers, and the figures follow this sequence: a slow run, a middling
/// one, a fast one, and the middling one from then on.
/// </para>
/// <list type="bullet">
/// <item><description>Latency: 75ms, 100ms, 150ms, then 100ms.</description></item>
/// <item><description>Download: 31,250 bytes, 125,000, 343,750, then 125,000 — each over 1 second, so 0.25, 1.0, 2.75 then 1.0 Mbps — from 40, 150, 400 then 150 requests, none failed.</description></item>
/// <item><description>Upload: 62,500 bytes, 375,000, 166,250, then 375,000 — each over 1 second, so 0.5, 3.0, 1.33 then 3.0 Mbps — from 12, 64, 30 then 64 requests, none failed.</description></item>
/// </list>
/// <para>
/// A caller screens the servers and then measures the winner, so both calls land within one test
/// and report that test's latency. Measuring without screening first reports the figure of the
/// test in progress, and before any screening that is the middling run.
/// </para>
/// </remarks>
public class VariableSpeedTester : ISpeedTestService
{
    private int callCount = 0;
    private readonly ISpeedTestService inner;

    /// <summary>
    /// Constructs a new <see cref="VariableSpeedTester"/> instance.
    /// </summary>
    public VariableSpeedTester()
    {
        // Create a custom speed test service that returns different results for each call
        inner = new SpeedTestMock
        {
            GetServersAsyncFunc = _ => Task.FromResult(new IServer[]
            {
                new Server { Location = "Test Location", Sponsor = "Test Sponsor", Url = "http://test.com" }
            }),

            GetFastestServerByLatencyAsyncFunc = (servers, _, _) =>
            {
                callCount++;

                // The first server is always the fastest
                var server = servers[0];

                return Task.FromResult(LatencyForCurrentTest(server));
            },

            // A caller screens the servers and then measures the winner, so both calls land within
            // one test and must report the figure that test stands for.
            GetServerLatencyAsyncFunc = (server, _, _) => Task.FromResult(LatencyForCurrentTest(server)),

            // A caller may name its own server instead of letting the tester choose one. The
            // server is built as the real provider builds it — the URL asked for and the sponsor
            // placeholder, with no location the caller never supplied.
            GetServerLatencyByServerUrlAsyncFunc = (serverUrl, _, _) =>
                Task.FromResult(LatencyForCurrentTest(new Server { Sponsor = "(Unknown)", Url = serverUrl })),

            GetDownloadSpeedAsyncFunc = (server, _, _) =>
            {
                // Call 1: 31,250 bytes in 1 second = 250,000 bits/second = 0.25 Mbps
                // Call 2: 125,000 bytes in 1 second = 1,000,000 bits/second = 1.0 Mbps
                // Call 3: 343,750 bytes in 1 second = 2,750,000 bits/second = 2.75 Mbps
                // Call 4+: 125,000 bytes in 1 second = 1,000,000 bits/second = 1.0 Mbps

                SpeedTestResult result = callCount switch
                {
                    1 => new SpeedTestResult { BytesProcessed = 31250, ElapsedMilliseconds = 1000, RequestsSucceeded = 40, RequestsFailed = 0 },
                    2 => new SpeedTestResult { BytesProcessed = 125000, ElapsedMilliseconds = 1000, RequestsSucceeded = 150, RequestsFailed = 0 },
                    3 => new SpeedTestResult { BytesProcessed = 343750, ElapsedMilliseconds = 1000, RequestsSucceeded = 400, RequestsFailed = 0 },
                    _ => new SpeedTestResult { BytesProcessed = 125000, ElapsedMilliseconds = 1000, RequestsSucceeded = 150, RequestsFailed = 0 }
                };
                return Task.FromResult(result);
            },

            GetUploadSpeedAsyncFunc = (server, _, _) =>
            {
                // Call 1: 62,500 bytes in 1 second = 500,000 bits/second = 0.5 Mbps
                // Call 2: 375,000 bytes in 1 second = 3,000,000 bits/second = 3.0 Mbps
                // Call 3: 166,250 bytes in 1 second = 1,330,000 bits/second = 1.33 Mbps
                // Call 4+: 375,000 bytes in 1 second = 3,000,000 bits/second = 3.0 Mbps

                SpeedTestResult result = callCount switch
                {
                    1 => new SpeedTestResult { BytesProcessed = 62500, ElapsedMilliseconds = 1000, RequestsSucceeded = 12, RequestsFailed = 0 },
                    2 => new SpeedTestResult { BytesProcessed = 375000, ElapsedMilliseconds = 1000, RequestsSucceeded = 64, RequestsFailed = 0 },
                    3 => new SpeedTestResult { BytesProcessed = 166250, ElapsedMilliseconds = 1000, RequestsSucceeded = 30, RequestsFailed = 0 },
                    _ => new SpeedTestResult { BytesProcessed = 375000, ElapsedMilliseconds = 1000, RequestsSucceeded = 64, RequestsFailed = 0 }
                };
                return Task.FromResult(result);
            }
        };
    }

    /// <summary>
    /// The latency the current speed test stands for, whether it is being screened or measured.
    /// </summary>
    private LatencyTestResult LatencyForCurrentTest(IServer server)
    {
        // Call 1: 75ms
        // Call 2: 100ms
        // Call 3: 150ms
        // Call 4+: 100ms

        long latencyMilliseconds = callCount switch
        {
            1 => 75,
            3 => 150,
            _ => 100
        };

        return new LatencyTestResult { Server = server, LatencyMilliseconds = latencyMilliseconds };
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return inner.GetServersAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(serverUrl, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(serverUrl, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return inner.GetFastestServerByLatencyAsync(servers, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return inner.GetFastestServerByLatencyAsync(servers, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(server, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(serverUrl, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return inner.GetFastestServerByLatencyAsync(servers, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, progress, diagnostics, cancellationToken);
    }
}
