using System.Text.Json;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
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


        // Display speed test result. Only a test that did not run is null (and so omitted); an
        // all-failed test reports its zero speed alongside the counts, keeping the JSON one shape.
        var latencyFormatted = !settings.NoLatency ? $"{fastest.LatencyMilliseconds} ms" : null;
        var downloadFormatted = settings.NoDownload
            ? null
            : downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);
        var uploadFormatted = settings.NoUpload
            ? null
            : uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale);

        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToString(settings.DateTimeFormat),
            Latency = latencyFormatted!,
            DownloadSpeed = downloadFormatted!,
            DownloadSucceeded = settings.NoDownload ? null : downloadResult.RequestsSucceeded,
            DownloadFailed = settings.NoDownload ? null : downloadResult.RequestsFailed,
            UploadSpeed = uploadFormatted!,
            UploadSucceeded = settings.NoUpload ? null : uploadResult.RequestsSucceeded,
            UploadFailed = settings.NoUpload ? null : uploadResult.RequestsFailed,
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
            ServerUrl = fastest.Server.Url,
            Download = settings.NoDownload ? null : downloadResult,
            Upload = settings.NoUpload ? null : uploadResult
        };
    }
}
