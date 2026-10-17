using System.Text.Json;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken);


        // A test that did not run is absent, never a zeroed result.
        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        // Perform speed test.
        if (!settings.NoDownload)
        {
            using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
            downloadResult = await speedTestClient.GetDownloadSpeedAsync(fastest.Server, null, download, cancellationToken);
        }
        if (!settings.NoUpload)
        {
            using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
            uploadResult = await speedTestClient.GetUploadSpeedAsync(fastest.Server, null, upload, cancellationToken);
        }


        // Display speed test result. JSON holds data, not presentation, so every speed and
        // latency is a plain number in a fixed unit and the unit options do not reach here.
        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToWholeSecond(),
            LatencyMilliseconds = !settings.NoLatency ? fastest.LatencyMilliseconds : null,
            DownloadSpeedBitsPerSecond = downloadResult?.GetBitsPerSecond(),
            DownloadSucceeded = downloadResult?.RequestsSucceeded,
            DownloadFailed = downloadResult?.RequestsFailed,
            UploadSpeedBitsPerSecond = uploadResult?.GetBitsPerSecond(),
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

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }
}
