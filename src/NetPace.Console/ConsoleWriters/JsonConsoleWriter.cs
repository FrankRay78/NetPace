using System.Text.Json;
using System.Text.Json.Serialization;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, settings.DownloadSizeMb, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, settings.UploadSizeMb, cancellationToken);


        // Display speed test result.
        var latencyFormatted = !settings.NoLatency ? $"{fastest.Latency} ms" : null;
        var downloadFormatted = !settings.NoDownload ? downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;
        var uploadFormatted = !settings.NoUpload ? uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale) : null;

        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToString(settings.DateTimeFormat),
            Latency = latencyFormatted!,
            DownloadSpeed = downloadFormatted!,
            UploadSpeed = uploadFormatted!
        };

        var options = new JsonSerializerOptions { WriteIndented = settings.JsonPretty, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        string jsonString = JsonSerializer.Serialize(jsonResult, options);

        console.WriteLine(jsonString);
    }
}
