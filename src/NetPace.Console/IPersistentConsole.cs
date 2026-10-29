namespace NetPace.Console;

/// <summary>
/// A console whose output outlives the run that wrote it, so a later run may add to what is
/// already there — a <c>--file</c> target being appended to, as against a terminal or a pipe.
/// </summary>
public interface IPersistentConsole
{
    /// <summary>
    /// Whether the destination held no content when this run opened it. A format carrying a header
    /// line writes it only where the destination started empty, so a log appended to across many
    /// runs keeps a single header at its top.
    /// </summary>
    bool StartedEmpty { get; }
}
