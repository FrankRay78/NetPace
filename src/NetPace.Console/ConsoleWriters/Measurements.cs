using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// Helper class for running the download and upload measurements of a single speed test, and
/// settling which of the three measurements the outcome carries.
/// </summary>
internal static class Measurements
{
    /// <summary>
    /// Performs the download and upload tests the settings ask for, each inside its own diagnostic
    /// test scope, and returns them with the latency already measured. A test switched off with
    /// <c>--no-latency</c>, <c>--no-download</c> or <c>--no-upload</c> is absent from the outcome,
    /// never a zeroed result.
    /// </summary>
    /// <remarks>
    /// Run here rather than in each of the line-based writers: <c>--minimal</c>, <c>--simple</c>,
    /// <c>--csv</c> and <c>--json</c> run the same tests, so which tests ran, the order they ran in
    /// and the scope each was recorded under are settled in one place. The one way they differ in
    /// what they measure, <c>--minimal</c> and <c>--simple</c> turning the under-load probes off, is
    /// decided in <c>Program.BuildSpeedTestSettings</c>, not here. The default writer keeps its own copy, because it
    /// feeds a live display as each transfer runs.
    /// </remarks>
    public static async Task<SpeedTestOutcome> PerformAsync(ISpeedTestService speedTestClient, LatencyTestResult latency, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(latency);
        ArgumentNullException.ThrowIfNull(settings);

        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        if (!settings.NoDownload)
        {
            using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
            downloadResult = await speedTestClient.GetDownloadSpeedAsync(latency.Server, null, download, cancellationToken);
        }

        if (!settings.NoUpload)
        {
            using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
            uploadResult = await speedTestClient.GetUploadSpeedAsync(latency.Server, null, upload, cancellationToken);
        }

        // Under --no-latency nothing was probed, so the latency is absent from the outcome rather
        // than reported as the zero the unmeasured result carries, on the same terms as a transfer
        // the run was told to skip.
        return SpeedTestOutcome.Create(settings.NoLatency ? null : latency, downloadResult, uploadResult);
    }
}
