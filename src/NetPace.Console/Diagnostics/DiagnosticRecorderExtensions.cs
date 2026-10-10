using System.Globalization;
using NetPace.Console.ConsoleWriters;

namespace NetPace.Console.Diagnostics;

/// <summary>
/// Records the diagnostics that every output format shares, so the four writers compose the same
/// records rather than each composing their own.
/// </summary>
internal static class DiagnosticRecorderExtensions
{
    /// <summary>
    /// Records the chosen server and the route that chose it.
    /// </summary>
    /// <remarks>
    /// <c>latency_ms</c> is the screening figure, because that is what the choice was based on, and
    /// is absent where latency played no part in choosing - under <c>--no-latency</c>, or when the
    /// user named the server. The measured figure appears in the latency test's own records.
    /// </remarks>
    public static void RecordServerSelected(this IDiagnosticRecorder recorder, ServerSelection selection)
    {
        recorder.Record(
            "server.selected",
            ("sponsor", selection.Result.Server.Sponsor),
            ("location", selection.Result.Server.Location),
            ("url", selection.Result.Server.Url),
            ("selection", selection.Route),
            ("latency_ms", selection.ScreeningLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture)));
    }
}
