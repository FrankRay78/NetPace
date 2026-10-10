namespace NetPace.Core.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> for tests: starts at a fixed instant and advances by a fixed step
/// on every read, so a sequence of stamped times is predictable without sleeping.
/// </summary>
/// <remarks>
/// <see cref="TimeZoneInfo.Utc"/> is reported as the local zone so that local-time stamps do not
/// vary with the machine running the suite.
/// </remarks>
internal sealed class DeterministicTimeProvider(DateTimeOffset start, TimeSpan step) : TimeProvider
{
    private int reads = -1;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow()
    {
        var read = Interlocked.Increment(ref reads);
        return start + (step * read);
    }
}
