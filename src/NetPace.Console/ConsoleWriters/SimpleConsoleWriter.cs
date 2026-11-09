using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The result as one named line per figure — <c>Ping</c>, <c>Download</c>, <c>Upload</c> — for a
/// script that reads output a line at a time, as <c>speedtest-cli</c>'s <c>--simple</c> did.
/// </summary>
/// <remarks>
/// Machine-readable, so the width limit comes off and nothing it writes is styled: the line shape
/// is the contract a reader parses, and a break or an escape sequence inside one would land in the
/// value. Every line carries a <c>name: value</c> label, the timestamp included, so one expression
/// reads any of them.
/// </remarks>
public sealed class SimpleConsoleWriter : IConsoleWriter
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


        // Display speed test result: one line per figure, in the order the tests ran, and no line
        // at all for a test that did not run. Runs under --count and --loop follow one another
        // with no separator, so a reader takes every line the same way whichever run wrote it.
        if (settings.IncludeTimestamp)
        {
            console.WriteLine($"Timestamp: {clock.Now.ToHumanReadable()}");
        }
        if (!settings.NoLatency)
        {
            console.WriteLine($"Ping: {fastest.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
        }
        if (downloadResult is not null)
        {
            console.WriteLine($"Download: {downloadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{downloadResult.GetFailureAnnotation()}");
        }
        if (uploadResult is not null)
        {
            console.WriteLine($"Upload: {uploadResult.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{uploadResult.GetFailureAnnotation()}");
        }

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }
}
