using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    /// <summary>
    /// Whether this run has settled its header line, which is not the same as having written one:
    /// a destination that already holds content is sent no header, and the run is still done
    /// deciding. One writer serves a whole run, so the run's first row is the one the header sits
    /// above, whatever happened before that row.
    /// </summary>
    private bool headerWritten;

    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken, CancellationToken timeLimitToken = default)
    {
        // Get the server to use for speed testing, then measure.
        (ChosenServer chosen, LatencyTestResult? latency) = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, chosen.Server, latency, settings, recorder, cancellationToken, timeLimitToken);

        // Keyed off the first data row, not the first attempt: a test abandoned by an exception
        // reaches no row, and the one after it must still find its column names above it.
        if (!headerWritten && !settings.NoCSVHeader)
        {
            string header = Record([
                "Timestamp",
                "LatencyMilliseconds",
                "JitterMilliseconds",
                "DownloadSpeedBitsPerSecond",
                "DownloadSucceeded",
                "DownloadFailed",
                "DownloadLatencyMilliseconds",
                "DownloadLowestLatencyMilliseconds",
                "DownloadHighestLatencyMilliseconds",
                "DownloadJitterMilliseconds",
                "UploadSpeedBitsPerSecond",
                "UploadSucceeded",
                "UploadFailed",
                "UploadLatencyMilliseconds",
                "UploadLowestLatencyMilliseconds",
                "UploadHighestLatencyMilliseconds",
                "UploadJitterMilliseconds",
                "IPAddress",
                "Hostname",
                "ServerLocation",
                "ServerSponsor",
                "ServerUrl",
                "DeviceId",
                .. settings.CustomFields.Select(field => field.Key)]);

            foreach (IAnsiConsole destination in HeaderDestinations(console))
            {
                destination.WriteLine(header);
            }

            headerWritten = true;
        }

        // An unmeasured figure leaves its cell empty rather than reporting zero, so nothing
        // charting the column reads an unmeasured line as a perfect one.
        console.WriteLine(Record([
            clock.Now.ToMachineReadable(),
            latency?.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            latency?.JitterMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.MinimumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.MaximumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.JitterMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.Latency?.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.Latency?.MinimumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.Latency?.MaximumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.Latency?.JitterMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            clientInfoProvider.GetIPAddress(),
            clientInfoProvider.GetHostname(),
            chosen.Server.Location,
            chosen.Server.Sponsor,
            chosen.Server.Url,
            settings.DeviceId ?? string.Empty,
            .. settings.CustomFields.Select(field => field.Value)]));

        return outcome;
    }

    /// <summary>
    /// The destinations that should receive the header line. A composite console is unpacked so
    /// each of its destinations is judged on its own, and one that did not start empty is left out.
    /// A console that is not a composite is its own only destination.
    /// </summary>
    private static IEnumerable<IAnsiConsole> HeaderDestinations(IAnsiConsole console)
    {
        IReadOnlyList<IAnsiConsole> destinations = console is CompositeAnsiConsole composite
            ? composite.Destinations
            : [console];

        return destinations.Where(destination => destination is not IPersistentConsole persistent || persistent.StartedEmpty);
    }

    /// <summary>
    /// Joins the fields of one CSV record with commas — the only separator CSV output uses — each
    /// field escaped so a standard reader sees one field per column.
    /// </summary>
    private static string Record(params string[] fields) => string.Join(',', fields.Select(ToField));

    /// <summary>
    /// Writes a single value as a CSV field. A value holding a comma or a double quote is wrapped in
    /// double quotes with any of its own doubled, as RFC 4180 asks, so a reader returns the original
    /// text and the row keeps one field per column. A line break is written as a space rather than
    /// quoted, so every record stays on one line whatever the machine reports. Anything else — every
    /// number, the timestamp, and an ordinary hostname — is written exactly as it arrives.
    /// Why the separator is fixed and why a line break is not quoted instead:
    /// docs/change-intent-records/2026-10-29-csv-is-always-comma-separated.md.
    /// </summary>
    private static string ToField(string value)
    {
        string oneLine = value
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        if (oneLine.Contains(',', StringComparison.Ordinal) || oneLine.Contains('"', StringComparison.Ordinal))
        {
            return $"\"{oneLine.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return oneLine;
    }
}
