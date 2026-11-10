using System.Globalization;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The <c>--simple</c> result: one <c>name: value</c> line per figure, in the order the tests ran.
/// </summary>
/// <remarks>
/// Written here rather than inside <see cref="SimpleConsoleWriter"/> because default output writes
/// these same lines wherever it cannot redraw — a pipe, a redirect, a <c>--file</c> target — and
/// the two must be the same lines, not two renderings that agree today. A test that did not run
/// contributes no line, so nothing here ever reports a zero for a measurement nobody asked for.
/// </remarks>
internal static class SimpleLines
{
    /// <summary>
    /// Writes the figures of one completed run to <paramref name="console"/>.
    /// </summary>
    /// <param name="console">Where the lines go.</param>
    /// <param name="latency">The chosen server's measured latency.</param>
    /// <param name="outcome">What the transfers measured.</param>
    /// <param name="settings">What the run was asked for.</param>
    /// <exception cref="ArgumentNullException">Any argument is <c>null</c>.</exception>
    public static void Write(IAnsiConsole console, LatencyTestResult latency, SpeedTestOutcome outcome, SpeedTestCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(latency);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(settings);

        // Runs under --count and --loop follow one another with no separator, so a reader takes
        // every line the same way whichever run wrote it. None of them carries the time: this
        // format is exactly the three lines speedtest-cli --simple writes, and --minimal is where
        // a timestamped line per run comes from.
        if (!settings.NoLatency)
        {
            console.WriteLine($"Ping: {latency.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
        }
        if (outcome.Download is SpeedTestResult download)
        {
            console.WriteLine($"Download: {download.GetFigure(settings)}");
        }
        if (outcome.Upload is SpeedTestResult upload)
        {
            console.WriteLine($"Upload: {upload.GetFigure(settings)}");
        }
    }
}
