using System.ComponentModel;

namespace NetPace.Console.Commands;

public sealed class ListServersCommandSettings : CommandSettings
{
    [CommandOption("-l|--latency")]
    [Description("Include server latency.")]
    public bool? ShowLatency { get; set; } = false;
}
