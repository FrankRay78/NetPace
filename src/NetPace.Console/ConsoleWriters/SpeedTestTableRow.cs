using System.Globalization;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// One row of <see cref="SpeedTestTable"/>: what a single measurement has reported so far.
/// </summary>
/// <remarks>
/// A figure the measurement has not reported stays absent rather than defaulting to zero, so the
/// table draws a blank cell where nothing was measured and a number only where something was. A
/// latency measurement fills fewer cells than a transfer does - no speed, and no count of failed
/// requests, since a failed probe is not a verdict on anything the user asked for - and it does so
/// simply by never reporting them.
/// </remarks>
/// <param name="table">The table the row belongs to, told whenever the row changes.</param>
/// <param name="name">What the row's Test cell names.</param>
internal sealed class SpeedTestTableRow(SpeedTestTable table, string name)
{
    /// <summary>What is being measured, as the Test column names it.</summary>
    internal string Name { get; } = name;

    internal SpeedTestTableRowState State { get; private set; } = SpeedTestTableRowState.Running;

    internal bool IsRunning => State == SpeedTestTableRowState.Running;

    internal int PercentageComplete { get; private set; }

    internal string Speed { get; private set; } = string.Empty;

    internal long? LatencyMilliseconds { get; private set; }

    internal long? JitterMilliseconds { get; private set; }

    /// <summary>
    /// How many of the measurement's requests failed, and how many were counted either way, or
    /// <see langword="null"/> where none failed. A request skipped at the byte cap is counted as
    /// neither, so the second figure is not the number attempted. Present only once the
    /// measurement has finished, because its own result is what says, and worded to match the
    /// annotation the simple lines carry.
    /// </summary>
    internal (int Failed, int Attempted)? FailedCount { get; private set; }

    internal string PercentageText => $"{PercentageComplete.ToString(CultureInfo.InvariantCulture)}%";

    internal string FailedText =>
        FailedCount is (int failed, int attempted)
            ? $"{failed.ToString(CultureInfo.InvariantCulture)} of {attempted.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

    /// <summary>Takes a latency measurement's running figures.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="progress"/> is <c>null</c>.</exception>
    internal void Report(LatencyTestProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        PercentageComplete = progress.PercentageComplete;
        LatencyMilliseconds = progress.LatencyMilliseconds;
        JitterMilliseconds = progress.JitterMilliseconds;
        table.Changed();
    }

    /// <summary>Takes a transfer's running figures.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="progress"/> is <c>null</c>.</exception>
    internal void Report(SpeedTestProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        PercentageComplete = progress.PercentageComplete;
        LatencyMilliseconds = progress.LatencyMilliseconds;
        JitterMilliseconds = progress.JitterMilliseconds;

        // Nothing is shown until a report carries both bytes and elapsed time, so a cell holds no
        // speed computed from nothing. A provider reporting only a percentage therefore shows no
        // speed until its result arrives.
        if (progress.BytesProcessed > 0 && progress.ElapsedMilliseconds > 0)
        {
            Speed = table.SpeedOf(progress);
        }

        table.Changed();
    }

    /// <summary>Ends the row on a latency measurement's own figures.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    internal void Settle(LatencyTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        PercentageComplete = 100;
        State = SpeedTestTableRowState.Succeeded;
        LatencyMilliseconds = result.LatencyMilliseconds;
        JitterMilliseconds = result.JitterMilliseconds;
        table.Changed();
    }

    /// <summary>
    /// Ends the row on a transfer's own figures: the speed it measured, the latency measured under
    /// it where there was any, and how many of its requests failed where any did.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <c>null</c>.</exception>
    internal void Settle(SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        PercentageComplete = 100;
        Speed = table.SpeedOf(result);

        // Read off the result rather than carried over from the last progress report, so the
        // figures the user watched end on the figures they are given - and a transfer that
        // measured none leaves its cells blank however many probes answered along the way.
        LatencyMilliseconds = result.Latency?.LatencyMilliseconds;
        JitterMilliseconds = result.Latency?.JitterMilliseconds;

        // Validity is read through the extensions that already own this policy, so the table and
        // the simple lines cannot come to disagree about what counts as a failure.
        State = result switch
        {
            _ when !result.HasFailures() => SpeedTestTableRowState.Succeeded,
            _ when result.IsAllFailed() => SpeedTestTableRowState.Failed,
            _ => SpeedTestTableRowState.PartiallyFailed,
        };

        if (result.HasFailures())
        {
            FailedCount = (result.RequestsFailed, result.RequestsSucceeded + result.RequestsFailed);
        }

        table.Changed();
    }

    /// <summary>
    /// Marks the row as a measurement that could not be taken at all, keeping whatever it had
    /// already reported: the row says what happened to it rather than disappearing.
    /// </summary>
    internal void Fail()
    {
        State = SpeedTestTableRowState.Failed;
        table.Changed();
    }
}
