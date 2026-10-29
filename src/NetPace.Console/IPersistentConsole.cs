namespace NetPace.Console;

/// <summary>
/// A console writing to a destination NetPace opens itself, so it can see what that destination
/// already holds before adding to it — a <c>--file</c> target, as against a terminal. Standard
/// output redirected with <c>&gt;&gt;</c> also outlives the run, but the shell opens it and NetPace
/// never sees the file, so it is deliberately not one of these.
/// </summary>
public interface IPersistentConsole
{
    /// <summary>
    /// Whether the destination held no content for this run to add to. A format carrying a header
    /// line writes it only where the destination started empty, so a log appended to across many
    /// runs keeps a single header at its top.
    /// </summary>
    bool StartedEmpty { get; }
}
