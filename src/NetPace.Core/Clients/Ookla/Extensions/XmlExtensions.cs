using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace NetPace.Core.Clients.Ookla.Extensions;

internal static class XmlExtensions
{
    internal static T? DeserializeFromXml<T>(this string data)
    {
        if (typeof(T) != typeof(OoklaServerList))
        {
            throw new NotSupportedException(
                $"DeserializeFromXml only supports {nameof(OoklaServerList)}; requested {typeof(T).FullName}.");
        }

        var doc = XDocument.Parse(data);
        var settings = doc.Element("settings")
            ?? throw new XmlException("Expected <settings> root element.");
        var serversElement = settings.Element("servers");

        var serverList = new OoklaServerList();

        if (serversElement is null)
        {
            return (T)(object)serverList;
        }

        var serverElements = serversElement.Elements("server").ToArray();
        if (serverElements.Length == 0)
        {
            serverList.Servers = Array.Empty<OoklaServer>();
            return (T)(object)serverList;
        }

        var servers = new OoklaServer[serverElements.Length];
        for (var i = 0; i < serverElements.Length; i++)
        {
            servers[i] = ParseServer(serverElements[i]);
        }

        serverList.Servers = servers;
        return (T)(object)serverList;
    }

    private static OoklaServer ParseServer(XElement element)
    {
        return new OoklaServer
        {
            Id = ParseRequiredInt(element, "id"),
            Location = ReadRequiredString(element, "name"),
            Country = element.Attribute("country")?.Value,
            Sponsor = ReadRequiredString(element, "sponsor"),
            Host = element.Attribute("host")?.Value,
            Url = ReadRequiredString(element, "url"),
            Latitude = ParseRequiredDouble(element, "lat"),
            Longitude = ParseRequiredDouble(element, "lon"),
        };
    }

    private static string ReadRequiredString(XElement element, string name)
    {
        var value = element.Attribute(name)?.Value
            ?? throw new XmlException($"Required attribute '{name}' missing on <{element.Name.LocalName}>.");
        return value;
    }

    private static int ParseRequiredInt(XElement element, string name)
    {
        var raw = ReadRequiredString(element, name);
        return int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static double ParseRequiredDouble(XElement element, string name)
    {
        var raw = ReadRequiredString(element, name);
        return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
    }
}
