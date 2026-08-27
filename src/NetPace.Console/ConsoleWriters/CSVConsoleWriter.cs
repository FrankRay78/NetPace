using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);
        if (fastest is null) return SpeedTestOutcome.NoServers;


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, cancellationToken);


        // Display speed test result. Count columns (which carry no units) sit adjacent to each
        // speed column so a single row distinguishes total from partial failure.
        var downloadSpeed = settings.CSVHeaderUnits
            ? downloadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale).speed
            : downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
        var uploadSpeed = settings.CSVHeaderUnits
            ? uploadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale).speed
            : uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

        var downloadHeader = settings.CSVHeaderUnits
            ? $"Download ({downloadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale).unit})"
            : "Download";
        var uploadHeader = settings.CSVHeaderUnits
            ? $"Upload ({uploadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale).unit})"
            : "Upload";
        var latencyValue = settings.CSVHeaderUnits ? $"{fastest.LatencyMilliseconds}" : $"{fastest.LatencyMilliseconds} ms";
        var latencyHeader = settings.CSVHeaderUnits ? "Latency (ms)" : "Latency";

        // Header row.
        if (initialSpeedTest)
        {
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

        return new SpeedTestOutcome
        {
            ServersFound = true,
            ServerUrl = fastest.Server.Url,
            Download = settings.NoDownload ? null : downloadResult,
            Upload = settings.NoUpload ? null : uploadResult
        };
    }
}
