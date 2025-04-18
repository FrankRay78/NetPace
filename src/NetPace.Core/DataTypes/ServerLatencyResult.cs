namespace NetPace.Core.DataTypes;

public sealed record ServerLatencyResult
{
    public required IServer Server { get; init; }
    public int Latency { get; init; }
}