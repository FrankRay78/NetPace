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
    /// Display minimal output in CSV format.
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
    /// The verbosity level.
    /// </summary>
    public required Verbosity Verbosity { get; init; }

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
}

public static class SpeedTestCommandSettingsExtensions
{
    /// <summary>
    /// Validate SpeedTestCommandSettings.
    /// </summary>
    /// <returns>Throws an exception if validation fails.</returns>
    public static void Validate(this SpeedTestCommandSettings settings)
    {
        if (settings.CSV && settings.CSVHeaderUnits && settings.SpeedScale == SpeedScale.Auto && (settings.Loop || settings.Count > 1))
        {
            throw new ArgumentException("The --unit-scale option must not be <Auto> for multiple speed tests (eg. --loop or --count).");
        }

        if (settings.NoLatency && settings.NoDownload && settings.NoUpload)
        {
            throw new ArgumentException("No tests selected. At least one of --no-latency, --no-download, or --no-upload must be omitted.");
        }
    }
}
