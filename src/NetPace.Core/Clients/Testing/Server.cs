namespace NetPace.Core.Clients.Testing;

/// <inheritdoc/>
public sealed record Server : IServer
{
    /// <inheritdoc/>
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required string Sponsor { get; set; }

    /// <inheritdoc/>
    public required string Url { get; set; }
}