using System.ComponentModel;

namespace NetPace.Console.Commands;

public sealed class ListServersCommandSettings : CommandSettings
{
    [CommandOption("-l|--latency")]
    [Description("Include server latency in the results.")]
    public bool? ShowLatency { get; set; } = false;

    [CommandOption("-f|--fastest")]
    [Description("Show the fastest server details, selected by lowest latency.")]
    public bool? Fastest { get; set; } = false;
}
