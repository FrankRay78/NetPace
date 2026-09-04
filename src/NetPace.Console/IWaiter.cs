namespace NetPace.Console;

/// <summary>
/// Interface for pausing for a specified time period.
/// </summary>
public interface IWaiter
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}

/// <inheritdoc/>
public sealed class Waiter : IWaiter
{
    public async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
    }
}

/// <summary>
/// A stub implementation of <see cref="IWaiter"/> that proceeds without delay.
/// </summary>
/// <remarks>
/// Useful when an actual delay would be problematic.
/// </remarks>
public sealed class NoDelayStub : IWaiter
{
    public int CallCount { get; private set; } = 0;

    public async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        CallCount++;

        await Task.Delay(TimeSpan.Zero, cancellationToken);
    }
}

public sealed class SelfCancellingWaiter : IWaiter
{
    public int CallCount { get; private set; } = 0;

    private readonly int maxCallCount;
    private readonly CancellationTokenSource cancellationTokenSource;

    public SelfCancellingWaiter(int maxCallCount, CancellationTokenSource cancellationTokenSource)
    {
        this.maxCallCount = maxCallCount;
        this.cancellationTokenSource = cancellationTokenSource;
    }

    public async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        CallCount++;

        if (CallCount >= maxCallCount)
        {
            cancellationTokenSource.Cancel();
        }

        await Task.Delay(TimeSpan.Zero, cancellationToken);
    }
}
