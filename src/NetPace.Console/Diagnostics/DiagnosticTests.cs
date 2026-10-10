namespace NetPace.Console.Diagnostics;

/// <summary>
/// The names the four tests of a run carry in diagnostics. Every request record repeats one of
/// these, so a grepped subset of a diagnostic log stays meaningful without its surrounding records.
/// </summary>
public static class DiagnosticTests
{
    /// <summary>
    /// The ranking pass that chooses a server. Distinct from <see cref="Latency"/>: screening is how
    /// a server is chosen, the latency test is what gets reported.
    /// </summary>
    public const string Screening = "screening";

    /// <summary>
    /// The latency measurement taken against the chosen server.
    /// </summary>
    public const string Latency = "latency";

    /// <summary>
    /// The download measurement.
    /// </summary>
    public const string Download = "download";

    /// <summary>
    /// The upload measurement.
    /// </summary>
    public const string Upload = "upload";
}
