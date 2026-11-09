using System.Collections.Concurrent;
using NetPace.Core.Clients.Testing;

namespace NetPace.Core.Tests;

/// <summary>
/// An <see cref="IDelayProvider"/> that holds every wait until the test lets it through, so a loop
/// paced by a delay runs exactly one iteration per release rather than on the wall clock.
/// </summary>
/// <remarks>
/// <see cref="DelayProviderStub"/> completes every wait at once, which turns a loop that waits
/// between iterations into one that spins for as long as the loop runs - fine for counting the waits
/// a fixed number of iterations asked for, useless for a loop whose iteration count is whatever the
/// interval and the surrounding operation's duration happen to produce. Holding each wait makes that
/// count the test's to decide.
/// </remarks>
internal sealed class GatedDelayProvider : IDelayProvider
{
    private readonly SemaphoreSlim asked = new(0);
    private readonly ConcurrentQueue<TaskCompletionSource> gates = new();
    private readonly ConcurrentQueue<int> intervals = new();

    /// <summary>
    /// The intervals waiting was asked for, in the order they were asked for.
    /// </summary>
    public IReadOnlyCollection<int> RequestedIntervals => intervals;

    /// <inheritdoc/>
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        intervals.Enqueue(milliseconds);
        gates.Enqueue(gate);
        asked.Release();

        // Cancellation is honoured, so a caller that stops waiting mid-wait is not held here until
        // the test happens to release the gate.
        return gate.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Waits until a wait has been asked for - it may not have been yet - and then lets it through.
    /// Returns <see langword="false"/> when nothing asked within <paramref name="hangGuard"/>, so a
    /// regression that never waits fails the test instead of hanging the run.
    /// </summary>
    public async Task<bool> ReleaseNextAsync(TimeSpan hangGuard)
    {
        if (!await asked.WaitAsync(hangGuard))
        {
            return false;
        }

        gates.TryDequeue(out TaskCompletionSource? gate);
        gate!.SetResult();
        return true;
    }
}
