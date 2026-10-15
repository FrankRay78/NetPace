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
    /// Display output as a single CSV row.
    /// </summary>
    public required bool CSV { get; init; }

    /// <summary>
    /// Single character delimiter to use in CSV output.
    /// </summary>
    public required char CSVDelimiter { get; init; }

    /// <summary>
    /// Display speed test units (eg. Mbps) in the CSV header row, not the data rows.
    /// </summary>
    public required bool CSVHeaderUnits { get; init; }

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
    /// Include a timestamp in the output.
    /// </summary>
    public required bool IncludeTimestamp { get; init; }

    /// <summary>
    /// The datetime format string, as defined by Microsoft.Net.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
    /// </remarks>
    public required string DateTimeFormat { get; init; }

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
    /// Display the result as a single compact line.
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
    /// Write per-request diagnostic detail about the run to the error stream.
    /// </summary>
    public required bool Diagnostics { get; init; }
}

public static class SpeedTestCommandSettingsExtensions
{
    /// <summary>
    /// A fixed instant used only to try a datetime format string out: fixed so the check cannot
    /// depend on the clock, and modern so it falls inside every culture's calendar range — which is
    /// why <see cref="FormatException"/> is the only exception the probe has to catch. It formats
    /// through the same current culture the writers do, so the check cannot accept a value that
    /// then fails at output time.
    /// </summary>
    private static readonly DateTime FormatProbeInstant = new DateTime(1980, 1, 1, 10, 5, 0);

    /// <summary>
    /// Validate SpeedTestCommandSettings.
    /// </summary>
    /// <returns>Throws an exception if validation fails.</returns>
    public static void Validate(this SpeedTestCommandSettings settings)
    {
        // Exactly one output format may be selected. --json-pretty shapes the JSON format rather
        // than selecting a second one, so it counts as JSON and stays usable alongside --json.
        // Checked here rather than as a parse validator: a command-level parse error discards every
        // option-level error, which hid an unparseable --unit and the --verbosity removal notice
        // whenever a format conflict was present too.
        var formatsSelected = (settings.CSV ? 1 : 0) + (settings.Json || settings.JsonPretty ? 1 : 0) + (settings.Minimal ? 1 : 0);
        if (formatsSelected > 1)
        {
            var switches = new List<string>();
            if (settings.CSV) switches.Add("--csv");
            if (settings.Json) switches.Add("--json");
            if (settings.JsonPretty) switches.Add("--json-pretty");
            if (settings.Minimal) switches.Add("--minimal");

            throw new ArgumentException($"Only one output format may be specified: {string.Join(", ", switches)}.");
        }

        if (settings.CSV && settings.CSVHeaderUnits && settings.SpeedScale == SpeedScale.Auto && (settings.Loop || settings.Count > 1))
        {
            throw new ArgumentException("The --unit-scale option must not be <Auto> for multiple speed tests (eg. --loop or --count).");
        }

        if (settings.NoLatency && settings.NoDownload && settings.NoUpload)
        {
            throw new ArgumentException("No tests selected. At least one of --no-latency, --no-download, or --no-upload must be omitted.");
        }

        // The format is checked whether or not this run would print a timestamp: a mistyped format
        // is a mistake either way, and deferring the check to the first run that uses it meant a
        // full measurement was spent before the user heard about it. Checked last so it does not
        // mask the more structural errors above.
        if (string.IsNullOrWhiteSpace(settings.DateTimeFormat))
        {
            throw new ArgumentException("The --datetimeformat value is blank. Pass a format such as \"yyyy-MM-dd HH:mm:ss\", or omit the option for the default.");
        }

        // Formatting is the only way to know a .NET format string is usable, so the check is an
        // attempt rather than a pattern match.
        try
        {
            _ = FormatProbeInstant.ToString(settings.DateTimeFormat);
        }
        catch (FormatException)
        {
            throw new ArgumentException($"The --datetimeformat value '{settings.DateTimeFormat}' is not a usable date and time format string. Pass a format such as \"yyyy-MM-dd HH:mm:ss\", or omit the option for the default.");
        }
    }
}
