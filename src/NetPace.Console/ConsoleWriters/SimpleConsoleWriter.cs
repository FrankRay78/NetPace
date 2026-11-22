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
/// value. Every line this writer writes carries a <c>name: value</c> label, so one expression reads
/// any of them, and a run that faults writes its unlabelled <c>Error:</c> message to the error
/// stream, so the stream a reader parses carries figures only.
/// <para>
/// These are also the lines default output writes wherever it cannot redraw, which is why the
/// lines themselves live in <see cref="SimpleLines"/>.
/// </para>
/// </remarks>
public sealed class SimpleConsoleWriter : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken, CancellationToken timeLimitToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Get the server to use for speed testing, then measure.
        (ChosenServer chosen, LatencyTestResult? latency) = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, chosen.Server, latency, settings, recorder, cancellationToken, timeLimitToken);

        SimpleLines.Write(console, latency, outcome, settings);

        return outcome;
    }
}
