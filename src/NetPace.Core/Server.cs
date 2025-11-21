namespace NetPace.Core;

/// <inheritdoc/>
public sealed record Server : IServer
{
    /// <inheritdoc/>
    public required string Location { get; set; }

    /// <inheritdoc/>
    public required string Sponsor { get; set; }

    /// <inheritdoc/>
    public required string Url { get; set; }
}