namespace NetPace.Console;

/// <summary>
/// Interface for obtaining the current date and time.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The current date and time, carrying the offset from UTC that it was observed at. The offset
    /// travels with the instant so that a machine-readable timestamp can name one unambiguous
    /// point in time, rather than a wall-clock reading whose zone the reader has to guess.
    /// </summary>
    DateTimeOffset Now { get; }
}

/// <summary>
/// Provides the system's current date and time.
/// </summary>
public sealed class Clock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}

/// <summary>
/// A stub implementation of <see cref="IClock"/> that returns a fixed date and time.
/// </summary>
/// <remarks>
/// Useful for testing scenarios that require predictable values. The offset is deliberately
/// non-zero, so a timestamp that silently dropped it would show up in a snapshot.
/// </remarks>
public sealed class ClockStub : IClock
{
    public DateTimeOffset Now => new DateTimeOffset(1980, 1, 1, 10, 5, 0, TimeSpan.FromHours(1));
}

/// <summary>
/// Increment the clock by 5 seconds on each call.
/// </summary>
public sealed class IncrementingClockStub : IClock
{
    private readonly DateTimeOffset startDate = new DateTimeOffset(1980, 1, 1, 10, 5, 0, TimeSpan.FromHours(1));

    private int callCount = -1;

    public DateTimeOffset Now
    {
        get
        {
            callCount++;

            return startDate.AddSeconds(callCount * 5);
        }
    }
}
