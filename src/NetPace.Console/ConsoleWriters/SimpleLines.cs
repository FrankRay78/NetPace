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
    /// <param name="latency">
    /// The chosen server's measured latency, or <c>null</c> where none was measured.
    /// </param>
    /// <param name="outcome">What the transfers measured.</param>
    /// <param name="settings">What the run was asked for.</param>
    /// <exception cref="ArgumentNullException"><paramref name="console"/>, <paramref name="outcome"/> or <paramref name="settings"/> is <c>null</c>.</exception>
    public static void Write(IAnsiConsole console, LatencyTestResult? latency, SpeedTestOutcome outcome, SpeedTestCommandSettings settings)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(settings);

        // No separator between runs under --count and --loop, and no timestamp on any line: this
        // format keeps the shape speedtest-cli --simple writes, and --minimal is where a
        // timestamped line per run comes from
        // (docs/change-intent-records/2026-11-10-minimal-returns-and-timestamp-goes.md).
        if (latency is LatencyTestResult measured)
        {
            console.WriteLine($"Ping: {measured.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture)} ms");
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
