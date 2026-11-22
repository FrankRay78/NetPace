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
    /// never a zeroed result - and so is one whose turn came after the run's time limit had been
    /// reached, which is a test that did not run and is reported as one.
    /// </summary>
    /// <remarks>
    /// Run here rather than in each of the line-based writers: <c>--minimal</c>, <c>--simple</c>,
    /// <c>--csv</c> and <c>--json</c> run the same tests, so which tests ran, the order they ran in
    /// and the scope each was recorded under are settled in one place. The one way they differ in
    /// what they measure, <c>--minimal</c> and <c>--simple</c> turning the under-load probes off, is
    /// decided in <c>Program.BuildSpeedTestSettings</c>, not here. The default writer keeps its own copy, because it
    /// feeds a live display as each transfer runs.
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

        // The limit is read before each test rather than once up front, because the test before it
        // may be what reached it. A test whose turn never came is left out of the outcome, so it is
        // reported wherever a test nobody asked for is reported, and the measurement that was cut
        // short still reports the figure it took.
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
