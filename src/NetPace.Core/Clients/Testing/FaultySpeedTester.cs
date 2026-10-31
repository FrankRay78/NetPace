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
/// Every figure comes from the service this one wraps — <see cref="SpeedTestStub"/> unless another
/// is supplied — so the tester adds failures without changing what succeeds. Which calls fail is
/// decided per server and per operation: by default, a latency probe to "Test Sponsor 2" fails,
/// and nothing else does. A transfer to that same server succeeds, because the default fault is
/// scoped to the one operation rather than to the server as a whole.
/// </para>
/// <para>
/// Screening is itself a latency probe, so a server whose latency probe fails drops out of
/// selection rather than being ranked, and a selection left with no reachable candidate fails
/// instead of returning one it could not measure.
/// </para>
/// <para>
/// A server named by URL is judged on the server the probe comes back with, not on the URL: the
/// sponsor is only known once the wrapped service has answered. The stub names such a server with
/// a placeholder sponsor, which the default fault does not match, so naming the unreachable
/// server's URL succeeds.
/// </para>
/// <para>
/// Listing servers never fails, so a consumer discovers the unreachable server and only finds out
/// it is unreachable when it measures.
/// </para>
/// <para>
/// Every failure the tester simulates surfaces as a plain <see cref="Exception"/>.
/// </para>
/// </remarks>
public class FaultySpeedTester : ISpeedTestService
{
    private readonly ISpeedTestService inner;
    private readonly Func<string?, string, bool> IsFaulted;

    /// <summary>
    /// Constructs a new <see cref="FaultySpeedTester"/> instance.
    /// </summary>
    /// <param name="inner">The service supplying the figures behind the failures. Defaults to a <see cref="SpeedTestStub"/>.</param>
    /// <param name="isFaulted">
    /// Decides whether a call fails, from the server's sponsor and the name of the operation being
    /// called. The operation is one of "GetServerLatencyAsync", "GetDownloadSpeedAsync" or
    /// "GetUploadSpeedAsync"; screening asks as "GetServerLatencyAsync", so no rule is ever passed
    /// "GetFastestServerByLatencyAsync". A rule supplied here replaces the default entirely, so the
    /// default fault on "Test Sponsor 2" no longer applies.
    /// </param>
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

    private bool CanBeScreened(IServer server) =>
        !IsFaulted(server.Sponsor, nameof(GetServerLatencyAsync));

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
        return GetFastestServerByLatencyAsync(servers, new NullProgress<SpeedTestProgress>(), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default)
    {
        return GetFastestServerByLatencyAsync(servers, progress, null, cancellationToken);
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

    /// <inheritdoc/>
    public Task<LatencyTestResult> GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetServerLatencyAsync));
        return inner.GetServerLatencyAsync(server, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LatencyTestResult> GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        var result = await inner.GetServerLatencyAsync(serverUrl, progress, diagnostics, cancellationToken);
        AssertNotFaulted(result.Server, nameof(GetServerLatencyAsync));
        return result;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Screening is itself a latency probe, so a server this tester cannot ping drops out of
    /// selection rather than being ranked, and a selection left with nothing reports that no
    /// servers are available.
    /// </remarks>
    public Task<LatencyTestResult> GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        var reachable = servers.Where(CanBeScreened).ToArray();

        if (reachable.Length == 0)
        {
            throw new Exception("No servers available");
        }

        return inner.GetFastestServerByLatencyAsync(reachable, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetDownloadSpeedAsync));
        return inner.GetDownloadSpeedAsync(server, progress, diagnostics, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress>? progress, IProgress<RequestDiagnostic>? diagnostics, CancellationToken cancellationToken = default)
    {
        AssertNotFaulted(server, nameof(GetUploadSpeedAsync));
        return inner.GetUploadSpeedAsync(server, progress, diagnostics, cancellationToken);
    }
}
