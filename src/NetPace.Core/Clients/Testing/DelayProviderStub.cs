namespace NetPace.Core.Clients.Testing;

/// <summary>
/// A stub implementation of <see cref="IDelayProvider"/> for testing purposes.
/// </summary>
/// <remarks>
/// Completes every wait at once, recording what was asked for so a test can verify it.
/// With <c>LatencyTestSettings.LoadedLatencyEnabled</c> on, the under-load probes it paces go out
/// back to back for the whole transfer; switch probing off when using this stub for a download or
/// an upload.
/// </remarks>
public sealed class DelayProviderStub : IDelayProvider
{
    private readonly List<int> requestedDelays = new();

    /// <summary>
    /// Gets the list of delay durations that were requested.
    /// </summary>
    public IReadOnlyList<int> RequestedDelays => requestedDelays;

    /// <summary>
    /// Gets the total number of delay requests made.
    /// </summary>
    public int DelayCallCount => requestedDelays.Count;

    /// <inheritdoc/>
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        requestedDelays.Add(milliseconds);
        return Task.CompletedTask;
    }
}
