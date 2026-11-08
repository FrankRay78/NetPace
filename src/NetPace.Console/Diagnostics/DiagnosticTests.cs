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

    /// <summary>
    /// Whether the named test is one that moves data, and so one whose latency probes are something
    /// it does alongside the measurement rather than the measurement itself.
    /// </summary>
    /// <remarks>
    /// Read off the name rather than passed in at each scope, so that the distinction is made in
    /// one place: a call site that forgot to say would otherwise log a probe as one of its test's
    /// own requests and quietly put the summary out by one.
    /// </remarks>
    public static bool MeasuresATransfer(string test) =>
        string.Equals(test, Download, StringComparison.Ordinal) ||
        string.Equals(test, Upload, StringComparison.Ordinal);
}
