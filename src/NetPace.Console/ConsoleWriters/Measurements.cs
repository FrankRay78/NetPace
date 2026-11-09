using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Helper class for running the download and upload measurements of a single speed test.
/// </summary>
internal static class Measurements
{
    /// <summary>
    /// Performs the download and upload tests the settings ask for, each inside its own diagnostic
    /// test scope. A test switched off with <c>--no-download</c> or <c>--no-upload</c> is absent
    /// from the outcome, never a zeroed result.
    /// </summary>
    /// <remarks>
    /// Run here rather than in each of the machine-readable writers: <c>--simple</c>, <c>--csv</c>
    /// and <c>--json</c> differ in what they write, never in what they measure, so which tests ran,
    /// the order they ran in and the scope each was recorded under are settled in one place. The
    /// default writer keeps its own copy, because it feeds a live display as each transfer runs.
    /// </remarks>
    public static async Task<SpeedTestOutcome> PerformAsync(ISpeedTestService speedTestClient, IServer server, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken)
    {
        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        if (!settings.NoDownload)
        {
            using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
            downloadResult = await speedTestClient.GetDownloadSpeedAsync(server, null, download, cancellationToken);
        }

        if (!settings.NoUpload)
        {
            using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
            uploadResult = await speedTestClient.GetUploadSpeedAsync(server, null, upload, cancellationToken);
        }

        return SpeedTestOutcome.Create(downloadResult, uploadResult);
    }
}
