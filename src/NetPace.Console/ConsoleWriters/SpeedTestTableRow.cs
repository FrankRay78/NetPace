using System.Globalization;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

// A figure the measurement has not reported stays absent rather than defaulting to zero, so the
// table draws a blank cell where nothing was measured. A latency measurement fills fewer cells than
// a transfer does, simply by never reporting them.
internal sealed class SpeedTestTableRow(SpeedTestTable table, string name)
{
    internal string Name { get; } = name;

    internal SpeedTestTableRowState State { get; private set; } = SpeedTestTableRowState.Running;

    internal bool IsRunning => State == SpeedTestTableRowState.Running;

    internal int PercentageComplete { get; private set; }

    internal string Speed { get; private set; } = string.Empty;

    internal long? LatencyMilliseconds { get; private set; }

    internal long? JitterMilliseconds { get; private set; }

    // A request skipped at the byte cap is counted as neither, so the second figure is not the
    // number attempted. Present only once the measurement has finished, and worded to match the
    // annotation the simple lines carry.
    internal (int Failed, int Attempted)? FailedCount { get; private set; }

    internal string PercentageText => $"{PercentageComplete.ToString(CultureInfo.InvariantCulture)}%";

    internal string FailedText =>
        FailedCount is (int failed, int attempted)
            ? $"{failed.ToString(CultureInfo.InvariantCulture)} of {attempted.ToString(CultureInfo.InvariantCulture)}"
            : string.Empty;

    internal void Report(LatencyTestProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        PercentageComplete = progress.PercentageComplete;
        LatencyMilliseconds = progress.LatencyMilliseconds;
        JitterMilliseconds = progress.JitterMilliseconds;
        table.Changed();
    }

    internal void Report(SpeedTestProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        PercentageComplete = progress.PercentageComplete;
        LatencyMilliseconds = progress.LatencyMilliseconds;
        JitterMilliseconds = progress.JitterMilliseconds;

        // Nothing is shown until a report carries both bytes and elapsed time, so a provider
        // reporting only a percentage shows no speed until its result arrives.
        if (progress.BytesProcessed > 0 && progress.ElapsedMilliseconds > 0)
        {
            Speed = table.SpeedOf(progress);
        }

        table.Changed();
    }

    internal void Settle(LatencyTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        PercentageComplete = 100;
        State = SpeedTestTableRowState.Succeeded;
        LatencyMilliseconds = result.LatencyMilliseconds;
        JitterMilliseconds = result.JitterMilliseconds;
        table.Changed();
    }

    internal void Settle(SpeedTestResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        PercentageComplete = 100;
        Speed = table.SpeedOf(result);

        // Read off the result rather than carried over from the last progress report, so the
        // figures the user watched end on the figures they are given.
        LatencyMilliseconds = result.Latency?.LatencyMilliseconds;
        JitterMilliseconds = result.Latency?.JitterMilliseconds;

        // Read through the extensions that already own this policy, so the table and the simple
        // lines cannot come to disagree about what counts as a failure.
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

    // Keeps whatever the row had already reported, so it says what happened rather than
    // disappearing.
    internal void Fail()
    {
        State = SpeedTestTableRowState.Failed;
        table.Changed();
    }
}
