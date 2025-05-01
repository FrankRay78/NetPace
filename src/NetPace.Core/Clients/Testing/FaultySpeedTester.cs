namespace NetPace.Core.Clients.Testing;

/// <summary>
/// An unreliable stub implementation of <see cref="ISpeedTestService"/> that
/// simulates network calls prone to error (e.g., timeouts, connection failures).
/// </summary>
/// <remarks>
/// <para>
/// Developers should used this service to test the fault tolerance of their application.
/// </para>
/// <para>
/// Default behavior throws an exception when "Test Sponsor 2" is passed into
/// the <see cref="ISpeedTestService.GetServerLatencyAsync(IServer)"/> method.
/// </para>
/// </remarks>
public class FaultySpeedTester : ISpeedTestService
{
    private readonly ISpeedTestService inner;
    private readonly Func<string?, string, bool> IsFaulted;

    public FaultySpeedTester(
        ISpeedTestService? inner = null,
        Func<string?, string, bool>? isFaulted = null)
    {
        this.inner = inner ?? new SpeedTestStub();
        this.IsFaulted = isFaulted ?? IsFaultedDefault;
    }

    private static bool IsFaultedDefault(string? sponsor, string methodName) =>
        string.Equals(sponsor, "Test Sponsor 2", StringComparison.Ordinal) &&
        string.Equals(methodName, nameof(GetServerLatencyAsync), StringComparison.Ordinal);

    private void AssertNotFaulted(IServer server, string methodName)
    {
        if (IsFaulted(server.Sponsor, methodName))
        { 
            throw new Exception($"Communication with '{server.Sponsor}' has failed");
        }
    }

    /// <inheritdoc/>
    public Task<IServer[]> GetServersAsync(CancellationToken cancellationToken = default)
    {
        return inner.GetServersAsync();
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetServerLatencyAsync));
        return inner.GetServerLatencyAsync(server);
    }

    /// <inheritdoc/>
    public Task<ServerLatencyResult> GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken cancellationToken = default)
    {
        return inner.GetFastestServerByLatencyAsync(servers);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetDownloadSpeedAsync));
        return inner.GetDownloadSpeedAsync(server);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetDownloadSpeedAsync));
        return inner.GetDownloadSpeedAsync(server, UpdateProgress);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetUploadSpeedAsync));
        return inner.GetUploadSpeedAsync(server);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, Action<SpeedTestProgress> UpdateProgress, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetUploadSpeedAsync));
        return inner.GetUploadSpeedAsync(server, UpdateProgress);
    }
}

