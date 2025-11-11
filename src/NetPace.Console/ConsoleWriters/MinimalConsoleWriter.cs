using ByteSizeLib;
using Humanizer;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class MinimalConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
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
            fastest = await speedTestClient.GetServerLatencyAsync(settings.ServerUrl, cancellationToken);
        }


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, cancellationToken);


        // Display speed test result.
        console.WriteLine(string.Join(", ", new[]
        {
            settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
            $"Latency: {fastest.Latency} ms",
            !settings.NoDownload ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null,
            !settings.NoUpload ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}" : null
        }.Where(s => !string.IsNullOrEmpty(s))));
    }
}
