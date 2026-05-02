namespace NetPace.Core.Clients.Ookla;

/// <summary>
/// Represents the root element of the XML server list provided by Ookla.
/// </summary>
public sealed class OoklaServerList
{
    /// <summary>
    /// Gets or sets the array of available speed test servers.
    /// </summary>
    public OoklaServer[]? Servers { get; set; }
}
