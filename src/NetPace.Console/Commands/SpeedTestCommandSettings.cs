using NetPace.Core;

namespace NetPace.Console.Commands;

/// <summary>
/// Settings for the SpeedTestCommand.
/// </summary>
public sealed class SpeedTestCommandSettings
{
    /// <summary>
    /// Performs the speed test on continuous loop.
    /// </summary>
    public required bool Loop { get; init; }

    /// <summary>
    /// Stop speed testing after this many times.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Time between multiple speed tests.
    /// </summary>
    public required TimeSpan Delay { get; init; }

    /// <summary>
    /// How long one speed test run may take, from choosing a server to the end of the upload test.
    /// Always a positive duration: a run always has a limit, and a user who wants a longer run sets
    /// a longer one. Each run of <c>--count</c> or <c>--loop</c> gets it afresh, and
    /// <see cref="Delay"/> does not count towards it.
    /// </summary>
    public required TimeSpan TimeLimit { get; init; }

    /// <summary>
    /// Display output as a single CSV row.
    /// </summary>
    public required bool CSV { get; init; }

    /// <summary>
    /// Leave the header row out of CSV output entirely, whatever its destination.
    /// </summary>
    public required bool NoCSVHeader { get; init; }

    /// <summary>
    /// Display output in Json format.
    /// </summary>
    public required bool Json { get; init; }

    /// <summary>
    /// Display output in Json format (pretty print).
    /// </summary>
    public required bool JsonPretty { get; init; }

    /// <summary>
    /// Do not perform latency test.
    /// </summary>
    public required bool NoLatency { get; init; }

    /// <summary>
    /// Do not perform download test.
    /// </summary>
    public required bool NoDownload { get; init; }

    /// <summary>
    /// Do not perform upload test.
    /// </summary>
    public required bool NoUpload { get; init; }

    /// <summary>
    /// The url of a specific speed test sever.
    /// </summary>
    public required string ServerUrl { get; init; }

    /// <summary>
    /// Which kind of speed test server the run tests against.
    /// </summary>
    public required Provider Provider { get; init; }

    /// <summary>
    /// The traffic-load profile that bundles per-request shape and total-byte cap defaults.
    /// </summary>
    public required Profile Profile { get; init; }

    /// <summary>
    /// Stop the download test after this many megabytes (IEC MiB).
    /// </summary>
    public required int DownloadSizeMb { get; init; }

    /// <summary>
    /// Stop the upload test after this many megabytes (IEC MiB).
    /// </summary>
    public required int UploadSizeMb { get; init; }

    /// <summary>
    /// The maximum number of transfers the download and upload tests each run at once, or
    /// <c>null</c> to leave the profile's own count in place.
    /// </summary>
    public required int? Connections { get; init; }

    /// <summary>
    /// The speed unit.
    /// </summary>
    public required SpeedUnit SpeedUnit { get; init; }

    /// <summary>
    /// The speed unit scale.
    /// </summary>
    public required SpeedScale SpeedScale { get; init; }

    /// <summary>
    /// The speed unit system.
    /// </summary>
    public required SpeedUnitSystem SpeedUnitSystem { get; init; }

    /// <summary>
    /// Display the result as one named line per figure.
    /// </summary>
    public required bool Simple { get; init; }

    /// <summary>
    /// Display the result as a single line per run, opening with the time it was taken.
    /// </summary>
    public required bool Minimal { get; init; }

    /// <summary>
    /// Write output to file.
    /// </summary>
    public required string OutputFile { get; init; }

    /// <summary>
    /// Determines file output behavior.
    /// </summary>
    public required FileMode FileModeValue { get; init; }

    /// <summary>
    /// Suppress all normal console output.
    /// </summary>
    public required bool Quiet { get; init; }

    /// <summary>
    /// Whether a measurement outcome causes a non-zero exit code (opt-in; default <see cref="FailOn.None"/>).
    /// </summary>
    public required FailOn FailOn { get; init; }

    /// <summary>
    /// The download speed the run must reach, or <see langword="null"/> when no target was given.
    /// </summary>
    public required MeasurementThreshold? MinimumDownload { get; init; }

    /// <summary>
    /// The upload speed the run must reach, or <see langword="null"/> when no target was given.
    /// </summary>
    public required MeasurementThreshold? MinimumUpload { get; init; }

    /// <summary>
    /// The idle latency the run must stay under, or <see langword="null"/> when no target was
    /// given.
    /// </summary>
    public required MeasurementThreshold? MaximumLatency { get; init; }

    /// <summary>
    /// Write per-request diagnostic detail about the run to the error stream, unless
    /// <see cref="DiagnosticsFile"/> names a file, which takes every record instead.
    /// </summary>
    public required bool Diagnostics { get; init; }

    /// <summary>
    /// Whether the run was given any target to hold itself to.
    /// </summary>
    public bool HasThreshold => MinimumDownload is not null || MinimumUpload is not null || MaximumLatency is not null;

    /// <summary>
    /// The file the diagnostic records are written to instead of the error stream, or an empty
    /// string when none was named. A path here switches diagnostics on by itself, so
    /// <see cref="Diagnostics"/> adds nothing alongside it.
    /// </summary>
    public required string DiagnosticsFile { get; init; }

    /// <summary>
    /// The label for this machine recorded in CSV and JSON output, or <see langword="null"/> when
    /// no label was given. Null and empty are distinct: CSV writes an empty cell for either, while
    /// JSON leaves the field out only when no label was given at all.
    /// </summary>
    public required string? DeviceId { get; init; }

    /// <summary>
    /// The user-defined pairs recorded in CSV and JSON output, in the order they were given on the
    /// command line. Empty when none were given.
    /// </summary>
    public required IReadOnlyList<CustomField> CustomFields { get; init; }
}

public static class SpeedTestCommandSettingsExtensions
{
    /// <summary>
    /// Validate SpeedTestCommandSettings.
    /// </summary>
    /// <returns>Throws an exception if validation fails.</returns>
    public static void Validate(this SpeedTestCommandSettings settings)
    {
        // Checked here, not as a parse validator: a command-level parse error discards every
        // option-level error, hiding an unparseable --unit whenever a format conflict was present.
        var formatsSelected = (settings.CSV ? 1 : 0) + (settings.Json || settings.JsonPretty ? 1 : 0) + (settings.Minimal ? 1 : 0) + (settings.Simple ? 1 : 0);
        if (formatsSelected > 1)
        {
            var switches = new List<string>();
            if (settings.CSV) switches.Add("--csv");
            if (settings.Json) switches.Add("--json");
            if (settings.JsonPretty) switches.Add("--json-pretty");
            if (settings.Minimal) switches.Add("--minimal");
            if (settings.Simple) switches.Add("--simple");

            throw new ArgumentException($"Only one output format may be specified: {string.Join(", ", switches)}.");
        }

        if (settings.NoLatency && settings.NoDownload && settings.NoUpload)
        {
            throw new ArgumentException("No tests selected. At least one of --no-latency, --no-download, or --no-upload must be omitted.");
        }

        // LibreSpeed publishes no server list, so its server is always named. Rejected before any
        // test runs, so the user is told what is missing rather than watching a run fail.
        if (settings.Provider == Provider.LibreSpeed && string.IsNullOrWhiteSpace(settings.ServerUrl))
        {
            throw new ArgumentException("A LibreSpeed server URL is needed: pass --server-url with the address LibreSpeed is installed at, such as --server-url http://pi.local:8080/.");
        }

        // A target answers one question about one run, and an exit code cannot say whether the
        // fifth test of fifty missed. Checked before anything is measured.
        if (settings.HasThreshold && (settings.Loop || settings.Count > 1))
        {
            throw new ArgumentException("A target checks a single run, so --min-download, --min-upload and --max-latency cannot be used with --loop or with --count above 1. Schedule repeated checks with cron, a systemd timer or Windows Task Scheduler.");
        }

        // A target for a test the same run turns off can never be answered either way, so it is
        // refused rather than quietly passing or quietly failing.
        if (settings.MaximumLatency is not null && settings.NoLatency)
        {
            throw new ArgumentException("--max-latency needs the latency test, so it cannot be used with --no-latency.");
        }

        if (settings.MinimumDownload is not null && settings.NoDownload)
        {
            throw new ArgumentException("--min-download needs the download test, so it cannot be used with --no-download.");
        }

        if (settings.MinimumUpload is not null && settings.NoUpload)
        {
            throw new ArgumentException("--min-upload needs the upload test, so it cannot be used with --no-upload.");
        }
    }
}
