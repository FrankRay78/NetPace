using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The server a run will use, and how it came to be chosen — settled before anything is measured
/// against it.
/// </summary>
/// <remarks>
/// Choosing and measuring are separate steps because the person watching wants the server named
/// while the first probe is still out: on a slow link that wait is seconds long, and a spinner
/// that says only "Choosing server" tells them nothing about what is being tested. The route is
/// carried alongside the server because the server alone cannot distinguish one the user named
/// from one screening picked, and a diagnostic log has to say which happened.
/// </remarks>
internal sealed record ChosenServer
{
    /// <summary>
    /// The route taken when screening chose the server by latency.
    /// </summary>
    public const string AutoLatencyRoute = "auto-latency";

    /// <summary>
    /// The route taken when the user named the server.
    /// </summary>
    public const string SpecifiedRoute = "specified";

    /// <summary>
    /// The route taken when latency was skipped and the first server offered was used.
    /// </summary>
    public const string FirstInListRoute = "first-in-list";

    /// <summary>
    /// Gets the server the run will measure against.
    /// </summary>
    public required IServer Server { get; init; }

    /// <summary>
    /// Gets how the server was chosen.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Gets the screening figure the choice was based on, or <see langword="null"/> where latency
    /// played no part in choosing. The measured figure appears in the latency test's own records.
    /// </summary>
    public long? ScreeningLatencyMilliseconds { get; init; }
}
