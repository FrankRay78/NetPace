namespace NetPace.Core;

/// <inheritdoc/>
public class Server : IServer
{
    /// <inheritdoc/>
    public string Location { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Sponsor { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Url { get; set; } = string.Empty;
}