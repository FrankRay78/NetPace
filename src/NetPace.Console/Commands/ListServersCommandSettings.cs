namespace NetPace.Console.Commands;

/// <summary>
/// Settings for the ListServersCommand.
/// </summary>
public sealed class ListServersCommandSettings
{
    /// <summary>
    /// Include server latency in the results.
    /// </summary>
    public bool? ShowLatency { get; set; } = false;

    /// <summary>
    /// Show the fastest server details, selected by lowest latency.
    /// </summary>
    public bool? Fastest { get; set; } = false;
}
