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
    /// test scope. A test switched off, or one whose turn came after the run's time limit, is
    /// absent from the outcome rather than a zeroed result.
    /// </summary>
    /// <remarks>
    /// Here rather than in each line-based writer, so which tests ran and their order are settled
    /// once. The default writer keeps its own copy, feeding a live display as each transfer runs.
    /// </remarks>
    /// <param name="latency">The latency already measured against <paramref name="server"/>, or
    /// <see langword="null"/> where none was: under <c>--no-latency</c>, or where the time limit
    /// was reached before any probe answered.</param>
    public static async Task<SpeedTestOutcome> PerformAsync(ISpeedTestService speedTestClient, IServer server, LatencyTestResult? latency, SpeedTestCommandSettings settings, IDiagnosticRecorder recorder, CancellationToken cancellationToken, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(settings);

        SpeedTestResult? downloadResult = null;
        SpeedTestResult? uploadResult = null;

        // Read before each test, because the test before it may be what reached the limit. One
        // whose turn never came is left out of the outcome entirely.
        if (!settings.NoDownload && !timeLimitToken.IsCancellationRequested)
        {
            using var download = DiagnosticTestScope.For(recorder, DiagnosticTests.Download);
            downloadResult = Measured(await speedTestClient.GetDownloadSpeedAsync(server, null, download, cancellationToken, timeLimitToken), timeLimitToken);
        }

        if (!settings.NoUpload && !timeLimitToken.IsCancellationRequested)
        {
            using var upload = DiagnosticTestScope.For(recorder, DiagnosticTests.Upload);
            uploadResult = Measured(await speedTestClient.GetUploadSpeedAsync(server, null, upload, cancellationToken, timeLimitToken), timeLimitToken);
        }

        return SpeedTestOutcome.Create(latency, downloadResult, uploadResult);
    }

    /// <summary>
    /// The transfer's result, or <see langword="null"/> where the run's time limit stopped it
    /// before any data arrived and before any request failed. A figure from no data is a
    /// measurement about nothing, and zero would read as a dead link, so the test is reported as
    /// one that did not run. A failed request is something the test did find out, so a result
    /// holding one is kept, to be reported and to trip <c>--fail-on</c> as it would with no limit.
    /// </summary>
    public static SpeedTestResult? Measured(SpeedTestResult result, CancellationToken timeLimitToken) =>
        timeLimitToken.IsCancellationRequested && result.BytesProcessed == 0 && result.RequestsFailed == 0 ? null : result;
}
