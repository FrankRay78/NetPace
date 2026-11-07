namespace NetPace.Core;

/// <summary>
/// A server used for network speed testing.
/// </summary>
public interface IServer
{
    /// <summary>
    /// Gets or sets the general location of the server, such as a city or region name.
    /// Useful for selecting a geographically relevant test server.
    /// </summary>
    string Location { get; set; }

    /// <summary>
    /// Gets or sets the name of the organization or entity sponsoring the server.
    /// This is often displayed to credit the infrastructure provider.
    /// </summary>
    /// <remarks>
    /// A server named by URL alone has no sponsor name, and carries the placeholder
    /// <c>[Unknown]</c> instead. Square brackets are Spectre.Console markup delimiters, so escape
    /// this value wherever it is written as markup.
    /// </remarks>
    string Sponsor { get; set; }

    /// <summary>
    /// Gets or sets the full URL of the server performing speed tests.
    /// This is typically an HTTP or HTTPS endpoint.
    /// </summary>
    string Url { get; set; }
}
