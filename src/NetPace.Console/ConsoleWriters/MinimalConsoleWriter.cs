using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

/// <summary>
/// The result as one line per run, opening with the time it was taken, for someone leaving NetPace
/// looping in a terminal and scanning the lines that scroll past, as <c>ping</c> is read.
/// </summary>
/// <remarks>
/// Machine-readable, so the width limit comes off and nothing it writes is styled: a line that
/// broke, or that carried an escape sequence, would be unreadable at exactly the moment this format
/// exists for, which is a column of lines compared by eye.
/// <para>
/// The timestamp is always there rather than behind a switch, because a line in a running log is of
/// little use without the time it was taken. A run that faults writes an unlabelled <c>Error:</c>
/// line to the same stream instead, with no timestamp in front of it: this format is a quick
/// shorthand, not a detailed log.
/// </para>
/// </remarks>
public sealed class MinimalConsoleWriter : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(settings);

        // Get the server to use for speed testing, then measure.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, fastest.Server, settings, recorder, cancellationToken);

        // The timestamp first, then a named field per figure in the order the tests ran. A test that
        // did not run contributes null rather than a field, so nothing here ever reports a zero for
        // a measurement nobody asked for.
        string?[] fields =
        [
            clock.Now.ToHumanReadable(),
            settings.NoLatency ? null : $"Latency: {fastest.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture)} ms",
            outcome.Download is SpeedTestResult download ? $"Download: {download.GetFigure(settings)}" : null,
            outcome.Upload is SpeedTestResult upload ? $"Upload: {upload.GetFigure(settings)}" : null,
        ];

        console.WriteLine(string.Join(", ", fields.Where(field => field is not null)));

        return outcome;
    }
}
