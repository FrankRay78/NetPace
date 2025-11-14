#pragma warning disable CS1591

namespace NetPace.Core.Clients.Ookla;

/// <inheritdoc/>
public sealed class Server : IServer
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("name")]
    public string Location { get; set; } = string.Empty;

    [XmlAttribute("country")]
    public string? Country { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("sponsor")]
    public string Sponsor { get; set; } = string.Empty;

    [XmlAttribute("host")]
    public string? Host { get; set; }

    /// <inheritdoc/>
    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    [XmlAttribute("lat")]
    public double Latitude { get; set; }

    [XmlAttribute("lon")]
    public double Longitude { get; set; }
}