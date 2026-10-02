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
        // Speed and header cells are formatted from the measurement, so they exist only where the test ran.
        var (downloadSpeed, downloadHeader) = FormatSpeedColumn(downloadResult, "Download", settings);
        var (uploadSpeed, uploadHeader) = FormatSpeedColumn(uploadResult, "Upload", settings);

        var latencyValue = settings.CSVHeaderUnits ? $"{fastest.LatencyMilliseconds}" : $"{fastest.LatencyMilliseconds} ms";
        var latencyHeader = settings.CSVHeaderUnits ? "Latency (ms)" : "Latency";

        // Header row.
        if (initialSpeedTest)
        {
            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                "Timestamp",
                !settings.NoLatency ? latencyHeader : null,
                downloadHeader,
                downloadResult is not null ? "DownloadSucceeded" : null,
                downloadResult is not null ? "DownloadFailed" : null,
                uploadHeader,
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

        return new SpeedTestOutcome
        {
            Download = downloadResult,
            Upload = uploadResult
        };
    }

    /// <summary>
    /// Formats the speed cell and its column header for one direction, or a pair of nulls where
    /// that test did not run and there is no measurement to report.
    /// </summary>
    private static (string? Speed, string? Header) FormatSpeedColumn(SpeedTestResult? result, string label, SpeedTestCommandSettings settings)
    {
        if (result is not { } measurement) return (null, null);

        if (!settings.CSVHeaderUnits)
        {
            return (measurement.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale), label);
        }

        var (speed, unit) = measurement.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
        return (speed, $"{label} ({unit})");
    }
}
