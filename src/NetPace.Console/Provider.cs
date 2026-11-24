namespace NetPace.Console;

/// <summary>
/// Which kind of speed test server a run tests against.
/// </summary>
/// <remarks>
/// A CLI concern, a library consumer constructing the provider they want directly: the factory
/// reads it off the settings record, so this chooses which record to build and nothing else.
/// </remarks>
public enum Provider
{
    /// <summary>Ookla's Speedtest servers, discovered or named. The default.</summary>
    Ookla = 0,

    /// <summary>A LibreSpeed server, always named with <c>--server-url</c>.</summary>
    LibreSpeed = 1,
}
