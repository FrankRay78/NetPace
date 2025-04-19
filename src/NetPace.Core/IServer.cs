namespace NetPace.Core;

/// <summary>
/// Represents a server used for network speed testing.
/// </summary>
public interface IServer
{
    string Name { get; set; }
    string Sponsor { get; set; }
    string Url { get; set; }
}