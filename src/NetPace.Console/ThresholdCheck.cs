using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Holding a finished run to the targets it was given, and saying what it missed.
/// </summary>
/// <remarks>
/// A figure exactly at its target meets it. A target with no figure at all to compare is missed:
/// a check that cannot see the line is not a check that passed.
/// <para>
/// The lines this produces are a word to the user about the run, not part of its result, so they go
/// to the error stream and leave every output format untouched.
/// </para>
/// </remarks>
internal static class ThresholdCheck
{
    /// <summary>
    /// The exit code a run that missed a target reports: distinct from <c>0</c> and from the
    /// <c>1</c> that means NetPace itself did not work, so a scheduler can tell a slow line from a
    /// broken check.
    /// </summary>
    public const int MissedExitCode = 3;

    /// <summary>
    /// The names the miss lines call the three measurements, matching what <c>--simple</c> output
    /// labels them, bar the latency, which that format still calls Ping.
    /// </summary>
    private const string LatencyMeasurement = "Latency";
    private const string DownloadMeasurement = "Download";
    private const string UploadMeasurement = "Upload";

    /// <summary>
    /// What the run missed, one line per target, in the order the tests run.
    /// </summary>
    /// <param name="outcome">
    /// What the run measured, or <see langword="null"/> where it ended before producing anything.
    /// </param>
    /// <param name="settings">What the run was asked for, carrying the targets to check.</param>
    /// <returns>One line per missed target; empty where every target was met or none was given.</returns>
    public static IReadOnlyList<string> Misses(SpeedTestOutcome? outcome, SpeedTestCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var misses = new List<string>();

        Check(misses, settings.MaximumLatency, LatencyMeasurement, outcome?.Latency?.LatencyMilliseconds, Direction.Maximum);
        Check(misses, settings.MinimumDownload, DownloadMeasurement, outcome?.Download?.GetBitsPerSecond(), Direction.Minimum);
        Check(misses, settings.MinimumUpload, UploadMeasurement, outcome?.Upload?.GetBitsPerSecond(), Direction.Minimum);

        return misses;
    }

    /// <summary>
    /// Adds the line for one target where the run missed it, and nothing where it met it or where
    /// no target was given for that measurement.
    /// </summary>
    private static void Check(List<string> misses, MeasurementThreshold? threshold, string measurement, long? figure, Direction direction)
    {
        if (threshold is null)
        {
            return;
        }

        var minimum = direction == Direction.Minimum;
        var side = minimum ? "below" : "above";
        var bound = minimum ? "minimum" : "maximum";

        if (figure is not long measured)
        {
            misses.Add($"{measurement} was not measured, so the {threshold.TargetFigure} {bound} is missed.");
            return;
        }

        var missed = minimum ? measured < threshold.Target : measured > threshold.Target;

        if (missed)
        {
            misses.Add($"{measurement} {threshold.Figure(measured)} is {side} the {threshold.TargetFigure} {bound}.");
        }
    }

    /// <summary>
    /// Which side of its target a figure has to stay on.
    /// </summary>
    private enum Direction
    {
        Minimum,
        Maximum
    }
}
