namespace NetPace.Core.Clients.Ookla;

public sealed class Server : IServer
{
    [XmlAttribute("id")]
    public int Id { get; set; }

    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("country")]
    public string? Country { get; set; }

    [XmlAttribute("sponsor")]
    public string Sponsor { get; set; } = string.Empty;

    [XmlAttribute("host")]
    public string? Host { get; set; }

    [XmlAttribute("url")]
    public string Url { get; set; } = string.Empty;

    [XmlAttribute("lat")]
    public double Latitude { get; set; }

    [XmlAttribute("lon")]
    public double Longitude { get; set; }
}