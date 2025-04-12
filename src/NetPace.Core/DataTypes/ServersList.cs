namespace NetPace.Core.DataTypes;

[XmlRoot("settings")]
public sealed class ServersList
{
    [XmlArray("servers")]
    [XmlArrayItem("server")]
    public Server[]? Servers { get; set; }
}