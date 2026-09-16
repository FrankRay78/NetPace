using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, cancellationToken);


        // Display speed test result. Count columns (which carry no units) sit adjacent to each
        // speed column so a single row distinguishes total from partial failure.
        // Derive each result once. Under --csv-header-units the unit moves into the header, so the
        // two parts are needed separately; otherwise the speed column carries the unit inline,
        // which is exactly the two parts joined by a space. Deriving both from one call is what
        // stops a row and its header disagreeing about the unit.
        var download = downloadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
        var upload = uploadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

        var downloadSpeed = settings.CSVHeaderUnits ? download.speed : $"{download.speed} {download.unit}";
        var uploadSpeed = settings.CSVHeaderUnits ? upload.speed : $"{upload.speed} {upload.unit}";
        var latencyValue = settings.CSVHeaderUnits ? $"{fastest.LatencyMilliseconds}" : $"{fastest.LatencyMilliseconds} ms";

        // Header row. The header strings are built here so that a run under --loop or --count does
        // not format and discard them on every iteration after the first.
        if (initialSpeedTest)
        {
            var downloadHeader = settings.CSVHeaderUnits ? $"Download ({download.unit})" : "Download";
            var uploadHeader = settings.CSVHeaderUnits ? $"Upload ({upload.unit})" : "Upload";
            var latencyHeader = settings.CSVHeaderUnits ? "Latency (ms)" : "Latency";

            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                "Timestamp",
                !settings.NoLatency ? latencyHeader : null,
                !settings.NoDownload ? downloadHeader : null,
                !settings.NoDownload ? "DownloadSucceeded" : null,
                !settings.NoDownload ? "DownloadFailed" : null,
                !settings.NoUpload ? uploadHeader : null,
                !settings.NoUpload ? "UploadSucceeded" : null,
                !settings.NoUpload ? "UploadFailed" : null,
                "IPAddress",
                "Hostname"
            }.Where(s => s is not null)));
        }

        // Data row.
        console.WriteLine(string.Join(settings.CSVDelimiter, new[]
        {
            clock.Now.ToString(settings.DateTimeFormat),
            !settings.NoLatency ? latencyValue : null,
            !settings.NoDownload ? downloadSpeed : null,
            !settings.NoDownload ? $"{downloadResult.RequestsSucceeded}" : null,
            !settings.NoDownload ? $"{downloadResult.RequestsFailed}" : null,
            !settings.NoUpload ? uploadSpeed : null,
            !settings.NoUpload ? $"{uploadResult.RequestsSucceeded}" : null,
            !settings.NoUpload ? $"{uploadResult.RequestsFailed}" : null,
            clientInfoProvider.GetIPAddress(),
            clientInfoProvider.GetHostname()
        }.Where(s => s is not null)));

        return SpeedTestOutcome.For(settings, downloadResult, uploadResult);
    }
}
