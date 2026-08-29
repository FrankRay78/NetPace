using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
{
    public async Task PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, cancellationToken);


        var downloadResult = new SpeedTestResult();
        var uploadResult = new SpeedTestResult();

        // Perform speed test.
        if (!settings.NoDownload) downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, cancellationToken);
        if (!settings.NoUpload) uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, cancellationToken);


        // Display speed test result.
        var latencyFormatted = !settings.NoLatency ? $"{fastest.LatencyMilliseconds} ms" : null;
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
            UploadSpeed = uploadFormatted!,
            IPAddress = clientInfoProvider.GetIPAddress(),
            Hostname = clientInfoProvider.GetHostname()
        };

        var typeInfo = settings.JsonPretty
            ? JsonResultIndentedContext.Default.JsonResult
            : JsonResultCompactContext.Default.JsonResult;
        string jsonString = JsonSerializer.Serialize(jsonResult, typeInfo);

        console.WriteLine(jsonString);
    }
}
