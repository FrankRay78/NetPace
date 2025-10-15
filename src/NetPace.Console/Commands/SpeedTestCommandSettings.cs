using System.ComponentModel;
using NetPace.Core;

namespace NetPace.Console.Commands;

public sealed class SpeedTestCommandSettings : CommandSettings
{
    [CommandOption("--loop")]
    [Description("Performs the speed test on continuous loop.")]
    [DefaultValue(false)]
    public bool Loop { get; set; }

    [CommandOption("--count")]
    [Description("Stop speed testing after this many times.")]
    public int Count { get; set; } = 1;

    [CommandOption("--delay")]
    [Description("Time between multiple speed tests (HH:MM:SS)")]
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    [CommandOption("--csv")]
    [Description("Display minimal output in CSV format (always includes timestamp).")]
    [DefaultValue(false)]
    public bool CSV { get; set; }

    [CommandOption("--csv-delimiter")]
    [Description("Single character delimiter to use in CSV output.")]
    [DefaultValue(',')]
    public char CSVDelimiter { get; set; }

    [CommandOption("--csv-header-units")]
    [Description("Display speed test units (eg. Mbps) in the CSV header row, not the data rows.\n--unit-scale must not be <Auto> for multiple speed tests (eg. --loop or --count)")]
    [DefaultValue(false)]
    public bool CSVHeaderUnits { get; set; }

    [CommandOption("--no-download")]
    [Description("Do not perform download test.")]
    [DefaultValue(false)]
    public bool NoDownload { get; set; }

    [CommandOption("--no-upload")]
    [Description("Do not perform upload test.")]
    [DefaultValue(false)]
    public bool NoUpload { get; set; }

    [CommandOption("--server")]
    [Description("The url of a specific speed test sever. \n'NetPace servers -l' will return your nearest servers.")]
    public string ServerUrl { get; set; } = string.Empty;

    [CommandOption("-t | --timestamp")]
    [Description("Include a timestamp in the output.")]
    [DefaultValue(false)]
    public bool IncludeTimestamp { get; set; }

    [CommandOption("--datetimeformat")]
    [Description("The datetime format string, as defined by Microsoft.Net.")]
    [DefaultValue("yyyy-MM-dd HH:mm:ss")]
    //ref: https://learn.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings
    public string? DateTimeFormat { get; set; }

    [CommandOption("--downloadsize")]
    [Description("Stop the download test after this many megabytes (IEC MiB).")]
    public int DownloadSizeMb { get; set; } = int.MaxValue;

    [CommandOption("--uploadsize")]
    [Description("Stop the upload test after this many megabytes (IEC MiB).")]
    public int UploadSizeMb { get; set; } = int.MaxValue;

    [CommandOption("-u | --unit")]
    [Description("The speed unit. <BitsPerSecond, BytesPerSecond>")]
    [DefaultValue(SpeedUnit.BitsPerSecond)]
    public SpeedUnit SpeedUnit { get; set; }

    [CommandOption("--unit-scale")]
    [Description("The speed unit scale. <Auto, Base, Kilo, Mega, Giga, Tera, Peta>")]
    [DefaultValue(SpeedScale.Auto)]
    public SpeedScale SpeedScale { get; set; }

    [CommandOption("--unit-system")]
    [Description("The speed unit system. <SI, IEC>\nSI steps up in powers of 1000 (KB, MB, GB), common in networking, while IEC uses powers of 1024 (KiB, MiB, GiB), standard in computing and storage.")]
    [DefaultValue(SpeedUnitSystem.SI)]
    public SpeedUnitSystem SpeedUnitSystem { get; set; }

    [CommandOption("--verbosity")]
    [Description("The verbosity level. <Minimal, Normal, Debug>\nMinimal is ideal for batch scripts and redirected output.")]
    [DefaultValue(Verbosity.Normal)]
    public Verbosity Verbosity { get; set; }


    /// <summary>
    /// Validate the settings.
    /// </summary>
    public override ValidationResult Validate()
    {
        // Add settings validations here.

        if (CSV && CSVHeaderUnits && SpeedScale == SpeedScale.Auto && (Loop || Count > 1))
        {
            //return ValidationResult.Error("The --csv-header-units option requires --unit-scale to be set to a specific value when performing multiple speed tests (eg. --loop or --count).");
            return ValidationResult.Error("The --unit-scale option cannot be Auto.");
        }

        return base.Validate();
    }
}
