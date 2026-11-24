using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Reading the server list file <c>--server-file</c> names: a JSON array of entries, each giving a
/// <c>url</c> and an optional <c>name</c>.
/// </summary>
/// <remarks>
/// Why the format is this and why only its structure is checked:
/// <c>docs/change-intent-records/2026-11-24-a-server-file-lists-the-candidates.md</c>.
/// </remarks>
internal static class ServerFileReader
{
    /// <summary>
    /// The field naming the server, the one field an entry must carry.
    /// </summary>
    private const string UrlField = "url";

    /// <summary>
    /// The field naming the server the way the user wants it shown.
    /// </summary>
    private const string NameField = "name";

    /// <summary>
    /// An example of the shape a file must have, for the messages that need to show it.
    /// </summary>
    private const string Example = """[{"url": "https://speedtest.example.com/"}]""";

    /// <summary>
    /// Reads the servers listed in <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The file to read, or an empty value where no file was named.</param>
    /// <returns>
    /// The servers listed, each carrying its entry's name as its sponsor and the placeholder
    /// <see cref="Server.UnknownSponsor"/> where the entry gave none. Empty where no file was
    /// named.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The file is missing, unreadable, malformed or empty, or one of its entries is not a server.
    /// </exception>
    public static IReadOnlyList<IServer> Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var json = ReadText(path);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException($"The server file {path} is empty. List at least one server, as {Example}.");
        }

        using var document = Parse(path, json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"The server file {path} must hold a JSON array of servers, as {Example}.");
        }

        var servers = new List<IServer>();
        var entry = 0;

        foreach (var element in document.RootElement.EnumerateArray())
        {
            entry++;
            servers.Add(ReadEntry(path, entry, element));
        }

        if (servers.Count == 0)
        {
            throw new ArgumentException($"The server file {path} lists no servers. List at least one, as {Example}.");
        }

        return servers;
    }

    private static string ReadText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Wrapped rather than reported raw: the framework's message says what went wrong but
            // not that it was the server file, and a run may name more than one file.
            throw new ArgumentException($"The server file {path} could not be read: {e.Message} Name a file you can read.", e);
        }
    }

    private static JsonDocument Parse(string path, string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"The server file {path} is not valid JSON: {e.Message} A server file holds an array of entries, as {Example}.", e);
        }
    }

    private static IServer ReadEntry(string path, int entry, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Entry {entry} of the server file {path} is not a server. Every entry is an object with a \"{UrlField}\" and an optional \"{NameField}\", as {Example}.");
        }

        string? url = null;
        string? name = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case UrlField:
                    url = Text(path, entry, property);
                    break;

                case NameField:
                    name = Text(path, entry, property);
                    break;

                default:
                    throw new ArgumentException($"Entry {entry} of the server file {path} carries the field \"{property.Name}\". An entry takes \"{UrlField}\" and an optional \"{NameField}\", and nothing else.");
            }
        }

        if (url is null)
        {
            throw new ArgumentException($"Entry {entry} of the server file {path} has no \"{UrlField}\" field. Every entry names its server by URL, as {Example}.");
        }

        return new Server
        {
            Url = url,
            Sponsor = name ?? Server.UnknownSponsor
        };
    }

    private static string Text(string path, int entry, JsonProperty property)
    {
        // What the value says is the run's business, not this reader's, but it has to be text at
        // all: a number or an object there is a file written wrong, not a server to go and try.
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Entry {entry} of the server file {path} gives \"{property.Name}\" as something other than text. Quote the value, as {Example}.");
        }

        return property.Value.GetString() ?? string.Empty;
    }
}
