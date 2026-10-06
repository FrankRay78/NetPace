using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);


        // A test that did not run is absent, never a zeroed result.
        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, cancellationToken);


        // Display speed test result. Count columns (which carry no units) sit adjacent to each
        // speed column so a single row distinguishes total from partial failure.
        var (downloadSpeed, downloadUnit) = FormatSpeedColumn(downloadResult, settings);
        var (uploadSpeed, uploadUnit) = FormatSpeedColumn(uploadResult, settings);

        var latencyValue = settings.CSVHeaderUnits ? $"{fastest.LatencyMilliseconds}" : $"{fastest.LatencyMilliseconds} ms";

        // Header row, emitted on the first iteration only. Each speed label takes its unit from
        // the same formatting call that produced the cell below it, so the header cannot disagree
        // with the row it heads. Later rows re-derive their own unit, so agreement across a
        // multi-row run rests on SpeedTestCommandSettings.Validate rejecting --csv-header-units
        // with an Auto scale under --loop/--count; revisit this if that rule is ever relaxed.
        if (initialSpeedTest)
        {
            var latencyHeader = settings.CSVHeaderUnits ? "Latency (ms)" : "Latency";

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                "Timestamp",
                !settings.NoLatency ? latencyHeader : null,
                downloadResult is not null ? ColumnHeader("Download", downloadUnit) : null,
                downloadResult is not null ? "DownloadSucceeded" : null,
                downloadResult is not null ? "DownloadFailed" : null,
                uploadResult is not null ? ColumnHeader("Upload", uploadUnit) : null,
                uploadResult is not null ? "UploadSucceeded" : null,
                uploadResult is not null ? "UploadFailed" : null,
                "IPAddress",
                "Hostname"
            }.Where(s => s is not null)));
        }

        // Data row.
        console.WriteLine(string.Join(settings.CSVDelimiter, new[]
        {
            clock.Now.ToString(settings.DateTimeFormat),
            !settings.NoLatency ? latencyValue : null,
            downloadSpeed,
            downloadResult?.RequestsSucceeded.ToString(),
            downloadResult?.RequestsFailed.ToString(),
            uploadSpeed,
            uploadResult?.RequestsSucceeded.ToString(),
            uploadResult?.RequestsFailed.ToString(),
            clientInfoProvider.GetIPAddress(),
            clientInfoProvider.GetHostname()
        }.Where(s => s is not null)));

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }

    /// <summary>
    /// Formats the speed cell for one direction, and the unit its column header must carry, or a
    /// pair of nulls where that test did not run and there is no measurement to report. The unit
    /// is null unless <c>--csv-header-units</c> moved it out of the cell and into the header.
    /// </summary>
    private static (string? Speed, string? Unit) FormatSpeedColumn(SpeedTestResult? result, SpeedTestCommandSettings settings)
    {
        if (result is null) return (null, null);

        if (!settings.CSVHeaderUnits)
        {
            return (result.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale), null);
        }

        return result.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
    }

    /// <summary>
    /// Composes a column header from its label and the unit the matching cell was formatted in.
    /// </summary>
    private static string ColumnHeader(string label, string? unit) => unit is null ? label : $"{label} ({unit})";
}
