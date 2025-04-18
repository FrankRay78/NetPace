namespace NetPace.Core.DataTypes;

public sealed record SpeedTestResult
{
    public long BytesProcessed { get; init; }
    public long ElapsedMilliseconds { get; init; }
}
