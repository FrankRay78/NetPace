namespace NetPace.Console;

/// <summary>
/// Interface for obtaining the current date and time.
/// </summary>
public interface IClock
{
    DateTime Now { get; }
}

/// <summary>
/// Provides the system's current date and time.
/// </summary>
public sealed class Clock : IClock
{
    public DateTime Now => DateTime.Now;
}

/// <summary>
/// A stub implementation of <see cref="IClock"/> that returns a fixed date and time.
/// </summary>
/// <remarks>
/// Useful for testing scenarios that require predictable values.
/// </remarks>
public sealed class ClockStub : IClock
{
    public DateTime Now => new DateTime(1980, 1, 1, 10, 5, 0);
}