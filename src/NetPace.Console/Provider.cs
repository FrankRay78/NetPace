namespace NetPace.Console;

/// <summary>
/// Which kind of speed test server a run tests against.
/// </summary>
/// <remarks>
/// A CLI concern rather than a library one: a library consumer constructs the provider they want
/// directly. The service factory reads the provider off the settings record it is handed, so this
/// enum chooses which record to build and nothing else.
/// </remarks>
public enum Provider
{
    /// <summary>Ookla's Speedtest servers, discovered or named. The default.</summary>
    Ookla = 0,

    /// <summary>A LibreSpeed server, always named with <c>--server-url</c>.</summary>
    LibreSpeed = 1,
}
