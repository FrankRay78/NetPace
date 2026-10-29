using System.Globalization;
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
            console.WriteLine(Record(
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
        // --unit-scale and --unit-system leave CSV untouched and every row carries the same units,
        // within a run and across runs.
        // A test that did not run leaves its cells empty; one that ran and failed entirely reports
        // a speed of 0 beside the request counts that say so.
        console.WriteLine(Record(
            clock.Now.ToMachineReadable(),
            !settings.NoLatency ? fastest.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) : string.Empty,
            downloadResult?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            downloadResult?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            downloadResult?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            uploadResult?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            uploadResult?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            uploadResult?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            clientInfoProvider.GetIPAddress(),
            clientInfoProvider.GetHostname()));

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }

    /// <summary>
    /// Joins the fields of one CSV record with commas — the only separator CSV output uses — each
    /// field escaped so a standard reader recovers it whole.
    /// </summary>
    private static string Record(params string[] fields) => string.Join(',', fields.Select(ToField));

    /// <summary>
    /// Writes a single value as a CSV field. A value holding a comma or a double quote is wrapped in
    /// double quotes with any of its own doubled, as RFC 4180 asks, so a reader returns the original
    /// text and the row keeps one field per column. A line break is written as a space rather than
    /// quoted, so every record stays on one line whatever the machine reports. Anything else — every
    /// number, the timestamp, and an ordinary hostname — is written exactly as it arrives.
    /// </summary>
    private static string ToField(string value)
    {
        var oneLine = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (!oneLine.Contains(',', StringComparison.Ordinal) && !oneLine.Contains('"', StringComparison.Ordinal))
        {
            return oneLine;
        }

        return $"\"{oneLine.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
