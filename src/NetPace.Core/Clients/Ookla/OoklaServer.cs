namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Represents an Ookla Speedtest server with geographical and network information.
/// </summary>
public sealed class OoklaServer : IServer
{
    /// <summary>
    /// Gets or sets the unique identifier for this Ookla server.
    /// </summary>
    [XmlAttribute("id")]
    public int Id { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("name")]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the country where the server is located.
    /// </summary>
    [XmlAttribute("country")]
    public string? Country { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("sponsor")]
    public string Sponsor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hostname of the server.
    /// </summary>
    [XmlAttribute("host")]
    public string? Host { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latitude coordinate of the server's physical location.
    /// </summary>
    [XmlAttribute("lat")]
    public double Latitude { get; set; }

    /// <summary>
    /// Gets or sets the longitude coordinate of the server's physical location.
    /// </summary>
    [XmlAttribute("lon")]
    public double Longitude { get; set; }
}