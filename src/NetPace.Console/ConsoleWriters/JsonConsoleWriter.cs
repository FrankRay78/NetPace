using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
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


        // Display speed test result.
        var latencyFormatted = !settings.NoLatency ? $"{fastest.LatencyMilliseconds} ms" : null;
        var downloadFormatted = downloadResult?.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
        var uploadFormatted = uploadResult?.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToString(settings.DateTimeFormat),
            Latency = latencyFormatted,
            DownloadSpeed = downloadFormatted,
            DownloadSucceeded = downloadResult?.RequestsSucceeded,
            DownloadFailed = downloadResult?.RequestsFailed,
            UploadSpeed = uploadFormatted,
            UploadSucceeded = uploadResult?.RequestsSucceeded,
            UploadFailed = uploadResult?.RequestsFailed,
            IPAddress = clientInfoProvider.GetIPAddress(),
            Hostname = clientInfoProvider.GetHostname()
        };

        var typeInfo = settings.JsonPretty
            ? JsonResultIndentedContext.Default.JsonResult
            : JsonResultCompactContext.Default.JsonResult;
        string jsonString = JsonSerializer.Serialize(jsonResult, typeInfo);

        console.WriteLine(jsonString);

        return new SpeedTestOutcome
        {
            Download = downloadResult,
            Upload = uploadResult
        };
    }
}
