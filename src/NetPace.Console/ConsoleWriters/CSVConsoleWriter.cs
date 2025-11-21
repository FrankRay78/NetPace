using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, cancellationToken);


        // Display speed test result.
        if (settings.CSVHeaderUnits)
        {
            var downloadFormattedParts = downloadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
            var uploadFormattedParts = uploadResult.GetSpeedStringParts(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

            // Header row.
            if (initialSpeedTest)
            {
                console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                {
                    "Timestamp",
                    !settings.NoLatency ? "Latency (ms)" : null,
                    !settings.NoDownload ? $"Download ({downloadFormattedParts.unit})" : null,
                    !settings.NoUpload ? $"Upload ({uploadFormattedParts.unit})" : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }

            // Data row.
            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                clock.Now.ToString(settings.DateTimeFormat),
                !settings.NoLatency ? $"{fastest.Latency}" : null,
                !settings.NoDownload ? downloadFormattedParts.speed : null,
                !settings.NoUpload ? uploadFormattedParts.speed : null
            }.Where(s => !string.IsNullOrEmpty(s))));
        }
        else
        {
            // Header row.
            if (initialSpeedTest)
            {
                console.WriteLine(string.Join(settings.CSVDelimiter, new[]
                {
                    "Timestamp",
                    !settings.NoLatency ? "Latency" : null,
                    !settings.NoDownload ? "Download" : null,
                    !settings.NoUpload ? "Upload" : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }

            // Data row.
            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                clock.Now.ToString(settings.DateTimeFormat),
                !settings.NoLatency ? $"{fastest.Latency} ms" : null,
                !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null,
                !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null
            }.Where(s => !string.IsNullOrEmpty(s))));
        }
    }
}
