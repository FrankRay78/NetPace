using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
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


        // Header row, emitted on the first iteration only. Every column is present whichever tests
        // ran, so a script can read by name or by position and an appended log keeps one shape.
        if (initialSpeedTest)
        {
            console.WriteLine(string.Join(settings.CSVDelimiter,
                "Timestamp",
                "LatencyMilliseconds",
                "DownloadSpeedBitsPerSecond",
                "DownloadSucceeded",
                "DownloadFailed",
                "UploadSpeedBitsPerSecond",
                "UploadSucceeded",
                "UploadFailed",
                "IPAddress",
                "Hostname"));
        }

        // Data row. Each measurement is a plain number in the unit its column names, so --unit,
        // --unit-scale and --unit-system leave CSV untouched and every row of every run matches.
        // A test that did not run leaves its cells empty; one that ran and failed entirely reports
        // a speed of 0 beside the request counts that say so.
        console.WriteLine(string.Join(settings.CSVDelimiter,
            clock.Now.ToString(settings.DateTimeFormat),
            !settings.NoLatency ? fastest.LatencyMilliseconds.ToString() : string.Empty,
            downloadResult?.GetBitsPerSecond().ToString() ?? string.Empty,
            downloadResult?.RequestsSucceeded.ToString() ?? string.Empty,
            downloadResult?.RequestsFailed.ToString() ?? string.Empty,
            uploadResult?.GetBitsPerSecond().ToString() ?? string.Empty,
            uploadResult?.RequestsSucceeded.ToString() ?? string.Empty,
            uploadResult?.RequestsFailed.ToString() ?? string.Empty,
            clientInfoProvider.GetIPAddress(),
            clientInfoProvider.GetHostname()));

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }
}
