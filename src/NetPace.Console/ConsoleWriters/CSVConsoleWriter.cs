using System.Text.Json;
using System.Text.Json.Serialization;
using ByteSizeLib;
using Humanizer;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, ISpeedTestService speedTestClient, IClock clock, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        ServerLatencyResult fastest;

        if (string.IsNullOrEmpty(settings.ServerUrl))
        {
            // Get the fastest speed test server.
            var servers = await speedTestClient.GetServersAsync(cancellationToken);
            fastest = await speedTestClient.GetFastestServerByLatencyAsync(servers, cancellationToken);
        }
        else
        {
            // User specified speed test server.
            var server = new Core.Clients.Ookla.Server() { Sponsor = "(Unknown)", Url = settings.ServerUrl };
            fastest = await speedTestClient.GetServerLatencyAsync(server, cancellationToken);
        }


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, cancellationToken);


        // Display speed test result
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
                    "Latency (ms)",
                    !settings.NoDownload ? $"Download ({downloadFormattedParts.unit})" : null,
                    !settings.NoUpload ? $"Upload ({uploadFormattedParts.unit})" : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }

            // Data row.
            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                clock.Now.ToString(settings.DateTimeFormat),
                $"{fastest.Latency}",
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
                    "Latency",
                    !settings.NoDownload ? "Download" : null,
                    !settings.NoUpload ? "Upload" : null
                }.Where(s => !string.IsNullOrEmpty(s))));
            }

            // Data row.
            console.WriteLine(string.Join(settings.CSVDelimiter, new[]
            {
                clock.Now.ToString(settings.DateTimeFormat),
                $"{fastest.Latency} ms",
                !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null,
                !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null
            }.Where(s => !string.IsNullOrEmpty(s))));
        }
    }
}
