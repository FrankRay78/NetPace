namespace NetPace.Core;

/// <summary>
/// Interface for providing delay functionality.
/// </summary>
/// <remarks>
/// This abstraction allows for testing code that uses delays without actually waiting.
/// </remarks>
public interface IDelayProvider
{
    /// <summary>
    /// Creates a task that completes after a specified time interval.
    /// </summary>
    /// <param name="milliseconds">The number of milliseconds to delay.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting.</param>
    /// <returns>A task that represents the time delay.</returns>
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="IDelayProvider"/> that uses <see cref="Task.Delay(int, CancellationToken)"/>.
/// </summary>
public sealed class DelayProvider : IDelayProvider
{
    /// <inheritdoc/>
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        return Task.Delay(milliseconds, cancellationToken);
    }
}
