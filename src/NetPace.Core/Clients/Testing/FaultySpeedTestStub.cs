namespace NetPace.Core.Clients.Testing;

/// <summary>
/// An unreliable stub implementation of <see cref="ISpeedTestService"/> for testing purposes.
/// Simulates network calls prone to error (e.g., timeouts, connection failures).
/// </summary>
/// <remarks>
/// Developers should used this service to test the fault tolerance of their application.
/// </remarks>
public sealed class FaultySpeedTestStub : ISpeedTestService
{
    private SpeedTestStub service = new SpeedTestStub();

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync()
    {
        return service.GetServersAsync();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server)
    {
        if (string.Equals(server!.Sponsor ?? "", "Test Sponsor 2"))
        {
            // Test Sponsor 2 is never pingable
            throw new Exception("Network error");
        }

        return service.GetServerLatencyAsync(server);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers)
    {
        return service.GetFastestServerByLatencyAsync(servers);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server)
    {
        return service.GetDownloadSpeedAsync(server);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<int> UpdateProgress)
    {
        return service.GetDownloadSpeedAsync(server, UpdateProgress);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server)
    {
        return service.GetUploadSpeedAsync(server);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<int> UpdateProgress)
    {
        return service.GetUploadSpeedAsync(server, UpdateProgress);
    }
}
