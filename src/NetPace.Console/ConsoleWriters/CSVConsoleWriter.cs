using System.Globalization;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class CSVConsoleWriter : IConsoleWriter
{
    // Settled, which is not the same as written: a destination that already holds content is sent
    // no header and the run is still done deciding.
    private bool headerWritten;

    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken, CancellationToken timeLimitToken = default)
    {
        (ChosenServer chosen, LatencyTestResult? latency) = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken, timeLimitToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, chosen.Server, latency, settings, recorder, cancellationToken, timeLimitToken);

        // Above the run's first data row rather than on its first attempt: a test abandoned by an
        // exception reaches no row, and keying the header off the attempt left the logs likeliest
        // to be lost that way - a loop started before the network is up - headerless. Which
        // destinations receive it at all is settled in HeaderDestinations
        // (docs/change-intent-records/2026-10-29-csv-header-is-decided-per-destination.md).
        //
        // Every built-in column is present whichever tests ran
        // (docs/change-intent-records/2026-10-16-csv-keeps-every-column-always.md), each figure
        // beside the measurement it belongs to
        // (docs/change-intent-records/2026-11-09-latency-figures-sit-beside-the-measurement.md),
        // and the custom columns last, so only the built-in prefix keeps one shape across runs.
        if (!headerWritten && !settings.NoCSVHeader)
        {
            string header = Record([
                "Timestamp",
                "LatencyMilliseconds",
                "JitterMilliseconds",
                "DownloadSpeedBitsPerSecond",
                "DownloadSucceeded",
                "DownloadFailed",
                "DownloadBytesProcessed",
                "DownloadLatencyMilliseconds",
                "DownloadLowestLatencyMilliseconds",
                "DownloadHighestLatencyMilliseconds",
                "DownloadJitterMilliseconds",
                "UploadSpeedBitsPerSecond",
                "UploadSucceeded",
                "UploadFailed",
                "UploadBytesProcessed",
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

        // Each measurement is a plain number in the unit its column names, so --unit, --unit-scale
        // and --unit-system leave CSV untouched. A cell is empty wherever a figure was not
        // measured, so nothing charting the column reads an unmeasured line as a perfect one; a
        // speed of 0 means nothing arrived at all.
        console.WriteLine(Record([
            clock.Now.ToMachineReadable(),
            latency?.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            latency?.JitterMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.BytesProcessed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.LatencyMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.MinimumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.MaximumLatencyMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Download?.Latency?.JitterMilliseconds.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.GetBitsPerSecond().ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.RequestsSucceeded.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.RequestsFailed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            outcome.Upload?.BytesProcessed.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
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

    // A composite console is unpacked so each destination is judged on its own: a log appended to
    // across runs must not gain a header line between every pair of rows.
    private static IEnumerable<IAnsiConsole> HeaderDestinations(IAnsiConsole console)
    {
        IReadOnlyList<IAnsiConsole> destinations = console is CompositeAnsiConsole composite
            ? composite.Destinations
            : [console];

        return destinations.Where(destination => destination is not IPersistentConsole persistent || persistent.StartedEmpty);
    }

    private static string Record(params string[] fields) => string.Join(',', fields.Select(ToField));

    // Quoting follows RFC 4180, but a line break is written as a space rather than quoted, so every
    // record stays on one line
    // (docs/change-intent-records/2026-10-29-csv-is-always-comma-separated.md).
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
