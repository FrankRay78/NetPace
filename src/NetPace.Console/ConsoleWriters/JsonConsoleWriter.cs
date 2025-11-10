using System.Text.Json;
using System.Text.Json.Serialization;
using ByteSizeLib;
using Humanizer;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
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
        var downloadFormatted = !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;
        var uploadFormatted = !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;

        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToString(settings.DateTimeFormat),
            Latency = $"{fastest.Latency} ms",
            DownloadSpeed = downloadFormatted!,
            UploadSpeed = uploadFormatted!
        };

        var options = new JsonSerializerOptions { WriteIndented = settings.JsonPretty, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        string jsonString = JsonSerializer.Serialize(jsonResult, options);

        console.WriteLine(jsonString);
    }
}
