using NetPace.Core.Clients.Ookla;

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
/// the GetServerLatencyAsync method.
/// </para>
/// </remarks>
public class FaultySpeedTester : ISpeedTestService
{
    private readonly ISpeedTestService inner;
    private readonly Func<string?, string, bool> IsFaulted;

    /// <summary>
    /// Constructs a new <see cref="FaultySpeedTester"/> instance.
    /// </summary>
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
        return inner.GetServersAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetServerLatencyAsync));
        return inner.GetServerLatencyAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetServerLatencyAsync));
        return inner.GetServerLatencyAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var result = await inner.GetServerLatencyAsync(serverUrl, cancellationToken);
        AssertNotFaulted(result.Server, nameof(GetServerLatencyAsync));
        return result;
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken cancellationToken = default)
    {
        var result = await inner.GetServerLatencyAsync(serverUrl, progress, cancellationToken);
        AssertNotFaulted(result.Server, nameof(GetServerLatencyAsync));
        return result;
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
        AssertNotFaulted(server, nameof(GetDownloadSpeedAsync));
        return inner.GetDownloadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetDownloadSpeedAsync));
        return inner.GetDownloadSpeedAsync(server, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetUploadSpeedAsync));
        return inner.GetUploadSpeedAsync(server, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetUploadSpeedAsync));
        return inner.GetUploadSpeedAsync(server, progress, cancellationToken);
    }
}
