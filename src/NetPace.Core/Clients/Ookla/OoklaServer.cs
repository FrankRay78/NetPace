namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Represents an Ookla Speedtest server with geographical and network information.
/// </summary>
public sealed class OoklaServer : IServer
{
    /// <summary>
    /// Gets or sets the unique identifier for this Ookla server.
    /// </summary>
    public int Id { get; set; }

    /// <inheritdoc/>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the country where the server is located.
    /// </summary>
    public string? Country { get; set; }

    /// <inheritdoc/>
    public string Sponsor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname of the server.
    /// </summary>
    public string? Host { get; set; }

    /// <inheritdoc/>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latitude coordinate of the server's physical location.
    /// </summary>
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude coordinate of the server's physical location.
    /// </summary>
    public double Longitude { get; set; }
}
