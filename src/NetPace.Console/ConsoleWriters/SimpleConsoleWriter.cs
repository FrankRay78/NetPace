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
/// value. Every line this writer writes carries a <c>name: value</c> label, the timestamp
/// included, so one expression reads any of them — but a run that faults writes an unlabelled
/// <c>Error:</c> message to the same stream, so a reader anchors its match (<c>grep '^Download:'</c>)
/// rather than trusting every line to be a figure.
/// </remarks>
public sealed class SimpleConsoleWriter : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing, then measure.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, fastest.Server, settings, recorder, cancellationToken);

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
        if (outcome.Download is SpeedTestResult download)
        {
            console.WriteLine($"Download: {Speed(download, settings)}");
        }
        if (outcome.Upload is SpeedTestResult upload)
        {
            console.WriteLine($"Upload: {Speed(upload, settings)}");
        }

        return outcome;
    }

    /// <summary>
    /// A measurement's figure as its line carries it: the speed in the unit the switches ask for,
    /// followed by the failed-request note where there is one.
    /// </summary>
    private static string Speed(SpeedTestResult result, SpeedTestCommandSettings settings) =>
        $"{result.GetSpeedString(settings.SpeedUnit, settings.SpeedUnitSystem, settings.SpeedScale)}{result.GetFailureAnnotation()}";
}
