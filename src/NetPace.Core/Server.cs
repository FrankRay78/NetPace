namespace NetPace.Core;

/// <inheritdoc/>
public class Server : IServer
{
    /// <summary>
    /// The placeholder sponsor, <c>[Unknown]</c>, carried by a server named by URL alone, for
    /// which no sponsor name is known.
    /// </summary>
    public const string UnknownSponsor = "[Unknown]";

    /// <inheritdoc/>
    public string Location { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Sponsor { get; set; } = string.Empty;

    /// <inheritdoc/>
    public string Url { get; set; } = string.Empty;
}
