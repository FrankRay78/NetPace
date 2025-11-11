namespace NetPace.Core.Clients.Testing;

/// <summary>
/// An implementation of <see cref="ISpeedTestService"/> that
/// simulates fluctuating speeds across successive network speed tests.
/// </summary>
public class VariableSpeedTester : ISpeedTestService
{
    private int callCount = 0;
    private readonly ISpeedTestService inner;

    public VariableSpeedTester()
    {
        // Create a custom speed test service that returns different results for each call
        inner = new SpeedTestMock
        {
            GetServersAsyncFunc = _ => Task.FromResult(new IServer[]
            {
                new Server { Location = "Test Location", Sponsor = "Test Sponsor", Url = "http://test.com" }
            }),

            GetFastestServerByLatencyAsyncFunc = (servers, _) => 
            {
                callCount++;

                // The first server is always the fastest
                var server = servers[0];

                ServerLatencyResult result = callCount switch
                {
                    1 => new ServerLatencyResult { Server = server, Latency = 75 },
                    2 => new ServerLatencyResult { Server = server, Latency = 100 },
                    3 => new ServerLatencyResult { Server = server, Latency = 150 },
                    _ => new ServerLatencyResult { Server = server, Latency = 100 },
                };
                return Task.FromResult(result);
            },

            GetDownloadSpeedAsyncFunc = (server, _, _, _) =>
            {
                // Call 1: 31,250 bytes in 1 second = 250,000 bits/second = 0.25 Mbps
                // Call 2: 125,000 bytes in 1 second = 1,000,000 bits/second = 1.0 Mbps
                // Call 3: 343,750 bytes in 1 second = 2,750,000 bits/second = 2.75 Mbps
                // Call 4+: 125,000 bytes in 1 second = 1,000,000 bits/second = 1.0 Mbps

                SpeedTestResult result = callCount switch
                {
                    1 => new SpeedTestResult { BytesProcessed = 31250, ElapsedMilliseconds = 1000 },
                    2 => new SpeedTestResult { BytesProcessed = 125000, ElapsedMilliseconds = 1000 },
                    3 => new SpeedTestResult { BytesProcessed = 343750, ElapsedMilliseconds = 1000 },
                    _ => new SpeedTestResult { BytesProcessed = 125000, ElapsedMilliseconds = 1000 }
                };
                return Task.FromResult(result);
            },

            GetUploadSpeedAsyncFunc = (server, _, _, _) =>
            {
                // Call 1: 62,500 bytes in 1 second = 500,000 bits/second = 0.5 Mbps
                // Call 2: 375,000 bytes in 1 second = 3,000,000 bits/second = 3.0 Mbps
                // Call 3: 166,250 bytes in 1 second = 1,330,000 bits/second = 1.33 Mbps
                // Call 4+: 375,000 bytes in 1 second = 3,000,000 bits/second = 3.0 Mbps

                SpeedTestResult result = callCount switch
                {
                    1 => new SpeedTestResult { BytesProcessed = 62500, ElapsedMilliseconds = 1000 },
                    2 => new SpeedTestResult { BytesProcessed = 375000, ElapsedMilliseconds = 1000 },
                    3 => new SpeedTestResult { BytesProcessed = 166250, ElapsedMilliseconds = 1000 },
                    _ => new SpeedTestResult { BytesProcessed = 375000, ElapsedMilliseconds = 1000 }
                };
                return Task.FromResult(result);
            }
        };
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return inner.GetServersAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        return inner.GetServerLatencyAsync(serverUrl, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return inner.GetFastestServerByLatencyAsync(servers, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, downloadSizeMb, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return inner.GetDownloadSpeedAsync(server, downloadSizeMb, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, uploadSizeMb, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, UpdateProgress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        return inner.GetUploadSpeedAsync(server, uploadSizeMb, UpdateProgress, cancellationToken);
    }
}

