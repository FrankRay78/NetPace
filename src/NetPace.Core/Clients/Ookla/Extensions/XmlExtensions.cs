namespace NetPace.Core.Clients.Ookla.Extensions;

internal static class XmlExtensions
{
    internal static T? DeserializeFromXml<T>(this string data)
    {
        var xmlSerializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(data);
        return (T?)xmlSerializer.Deserialize(reader);
    }
}
