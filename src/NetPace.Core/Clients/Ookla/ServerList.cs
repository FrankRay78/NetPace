namespace NetPace.Core.Clients.Ookla;

[XmlRoot("settings")]
public sealed class ServerList
{
    [XmlArray("servers")]
    [XmlArrayItem("server")]
    public Server[]? Servers { get; set; }
}