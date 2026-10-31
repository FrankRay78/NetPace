namespace NetPace.Core.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock stands still until a test moves it, so an elapsed-time
/// measurement taken through it is exactly what the test said it was rather than a wall-clock
/// reading of how long the machine happened to take.
/// </summary>
/// <remarks>
/// <see cref="TimeZoneInfo.Utc"/> is reported as the local zone so that local-time stamps do not
/// vary with the machine running the suite. <see cref="TimeProvider.GetTimestamp"/> is overridden
/// alongside <see cref="TimeProvider.GetUtcNow"/> because the base implementation reads the system
/// stopwatch: a double that moved only <see cref="TimeProvider.GetUtcNow"/> would control the
/// stamps on a record while its durations still came off the wall clock.
/// </remarks>
internal sealed class ControlledTimeProvider(DateTimeOffset start) : TimeProvider
{
    private long nowTicks = start.UtcTicks;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>
    /// One tick per timestamp unit, so <see cref="TimeProvider.GetElapsedTime(long)"/> converts
    /// the difference between two readings without scaling and therefore without rounding.
    /// </summary>
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Interlocked.Read(ref nowTicks);

    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref nowTicks), TimeSpan.Zero);

    /// <summary>
    /// Moves the clock on by <paramref name="amount"/>, as the only way time passes on it.
    /// </summary>
    public void Advance(TimeSpan amount) => Interlocked.Add(ref nowTicks, amount.Ticks);
}
