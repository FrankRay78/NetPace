namespace NetPace.Core;

/// <summary>
/// Represents a server used for network speed testing.
/// </summary>
/// <remarks>
/// Contains the minimum set of properties to represent a server.
/// </remarks>
public interface IServer
{
    string? Name { get; set; }
    string? Sponsor { get; set; }
    string? Url { get; set; }
}