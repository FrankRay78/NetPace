namespace NetPace.Core.Extensions;

public static class StringExtensions
{
    public static T? DeserializeFromXml<T>(this string data)
    {
        var xmlSerializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(data);
        return (T?)xmlSerializer.Deserialize(reader);
    }

    public static string Append(this string originalString, string value)
    {
        return originalString + value;
    }
}