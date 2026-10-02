using ByteSizeLib;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class MinimalConsoleWriter : IConsoleWriter
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
        console.WriteLine(string.Join(", ", new[]
        {
            settings.IncludeTimestamp ? clock.Now.ToString(settings.DateTimeFormat) : null,
            !settings.NoLatency ? $"Latency: {fastest.LatencyMilliseconds} ms" : null,
            downloadResult is not null ? $"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{downloadResult.GetFailureAnnotation()}" : null,
            uploadResult is not null ? $"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{uploadResult.GetFailureAnnotation()}" : null
        }.Where(s => !string.IsNullOrEmpty(s))));

        return new SpeedTestOutcome
        {
            Download = downloadResult,
            Upload = uploadResult
        };
    }
}
