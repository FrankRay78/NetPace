namespace NetPace.Console.Diagnostics;

/// <summary>
/// The clock that stamps diagnostic records.
/// </summary>
/// <remarks>
/// Deliberately separate from the <see cref="IClock"/> that stamps the result: the test clocks
/// count their reads, so one shared instance would let diagnostics shift a result's timestamp.
/// </remarks>
public interface IDiagnosticClock
{
    /// <summary>
    /// Gets the current date and time.
    /// </summary>
    DateTime Now { get; }
}

/// <summary>
/// Provides the system's current date and time for diagnostic records.
/// </summary>
public sealed class DiagnosticClock : IDiagnosticClock
{
    /// <inheritdoc />
    public DateTime Now => DateTime.Now;
}

/// <summary>
/// A stub implementation of <see cref="IDiagnosticClock"/> that advances five seconds on each read,
/// so a diagnostic snapshot shows the order records were written in without depending on real time.
/// </summary>
public sealed class IncrementingDiagnosticClockStub : IDiagnosticClock
{
    private static readonly DateTime StartDate = new DateTime(1980, 1, 1, 10, 5, 0);

    private int reads = -1;

    /// <inheritdoc />
    public DateTime Now
    {
        get
        {
            reads++;

            return StartDate.AddSeconds(reads * 5);
        }
    }
}
