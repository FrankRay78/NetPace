using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The server a run will use, and how it came to be chosen.
/// </summary>
/// <remarks>
/// The route is carried alongside the result because a bare <see cref="LatencyTestResult"/> cannot
/// distinguish a server the user named from one screening picked, and a diagnostic log has to say
/// which happened.
/// </remarks>
internal sealed record ServerSelection
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
    /// Gets the chosen server and the latency to report for it.
    /// </summary>
    public required LatencyTestResult Result { get; init; }

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
