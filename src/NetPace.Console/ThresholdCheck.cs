using NetPace.Core;

namespace NetPace.Console;

/// <summary>
/// Holding a finished run to the targets it was given, and saying what it missed.
/// </summary>
/// <remarks>
/// A target is missed when the measured figure is below a minimum or above a maximum; a figure
/// exactly at its target meets it. A test the run asked for but that produced no figure at all,
/// because every request failed or no server could be reached, misses its target too: a check that
/// cannot see the line is not a check that passed.
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

    private static void Check(List<string> misses, MeasurementThreshold? threshold, string measurement, long? figure, Direction direction)
    {
        if (threshold is null)
        {
            return;
        }

        var side = direction == Direction.Minimum ? "below" : "above";
        var bound = direction == Direction.Minimum ? "minimum" : "maximum";

        if (figure is not long measured)
        {
            misses.Add($"{measurement} was not measured, so it is {side} the {threshold.TargetFigure} {bound}.");
            return;
        }

        var missed = direction == Direction.Minimum
            ? measured < threshold.Target
            : measured > threshold.Target;

        if (!missed)
        {
            return;
        }

        misses.Add($"{measurement} {threshold.Figure(measured)} is {side} the {threshold.TargetFigure} {bound}.");
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
