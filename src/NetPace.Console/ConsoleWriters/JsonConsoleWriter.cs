using System.Text.Json;
using NetPace.Console.Diagnostics;
using NetPace.Core;

namespace NetPace.Console.ConsoleWriters;

public sealed class JsonConsoleWriter : IConsoleWriter
{
    /// <inheritdoc/>
    public bool MachineReadable => true;

    public async Task<SpeedTestOutcome> PerformSpeedTestAsync(bool initialSpeedTest, IAnsiConsole console, IClock clock, IClientInfoProvider clientInfoProvider, IDiagnosticRecorder recorder, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken)
    {
        // Get the server to use for speed testing, then measure.
        var fastest = await ServerSelector.GetServerAsync(speedTestClient, settings, recorder, cancellationToken);
        var outcome = await Measurements.PerformAsync(speedTestClient, fastest.Server, settings, recorder, cancellationToken);

        // Display speed test result. JSON holds data, not presentation, so every speed and
        // latency is a plain number in a fixed unit and the unit options do not reach here.
        var jsonResult = new JsonResult
        {
            ServerLocation = fastest.Server.Location,
            ServerSponsor = fastest.Server.Sponsor,
            ServerUrl = fastest.Server.Url,
            Timestamp = clock.Now.ToWholeSecond(),
            LatencyMilliseconds = !settings.NoLatency ? fastest.LatencyMilliseconds : null,
            JitterMilliseconds = !settings.NoLatency ? fastest.JitterMilliseconds : null,
            DownloadSpeedBitsPerSecond = outcome.Download?.GetBitsPerSecond(),
            DownloadSucceeded = outcome.Download?.RequestsSucceeded,
            DownloadFailed = outcome.Download?.RequestsFailed,
            DownloadLatencyMilliseconds = outcome.Download?.Latency?.LatencyMilliseconds,
            DownloadLowestLatencyMilliseconds = outcome.Download?.Latency?.MinimumLatencyMilliseconds,
            DownloadHighestLatencyMilliseconds = outcome.Download?.Latency?.MaximumLatencyMilliseconds,
            DownloadJitterMilliseconds = outcome.Download?.Latency?.JitterMilliseconds,
            UploadSpeedBitsPerSecond = outcome.Upload?.GetBitsPerSecond(),
            UploadSucceeded = outcome.Upload?.RequestsSucceeded,
            UploadFailed = outcome.Upload?.RequestsFailed,
            UploadLatencyMilliseconds = outcome.Upload?.Latency?.LatencyMilliseconds,
            UploadLowestLatencyMilliseconds = outcome.Upload?.Latency?.MinimumLatencyMilliseconds,
            UploadHighestLatencyMilliseconds = outcome.Upload?.Latency?.MaximumLatencyMilliseconds,
            UploadJitterMilliseconds = outcome.Upload?.Latency?.JitterMilliseconds,
            IPAddress = clientInfoProvider.GetIPAddress(),
            Hostname = clientInfoProvider.GetHostname(),
            DeviceId = settings.DeviceId,
            CustomFields = GroupCustomFields(settings.CustomFields)
        };

        var typeInfo = settings.JsonPretty
            ? JsonResultIndentedContext.Default.JsonResult
            : JsonResultCompactContext.Default.JsonResult;
        string jsonString = JsonSerializer.Serialize(jsonResult, typeInfo);

        console.WriteLine(jsonString);

        return outcome;
    }

    /// <summary>
    /// Groups the custom fields into one object, so a custom key can never replace a built-in
    /// field. A key given more than once keeps the last value, because a JSON object cannot hold it
    /// twice; CSV, which can, writes every column as given. Returns <see langword="null"/> when
    /// there are none, so the object is left out rather than written empty.
    /// </summary>
    private static Dictionary<string, string>? GroupCustomFields(IReadOnlyList<CustomField> customFields)
    {
        if (customFields.Count == 0)
        {
            return null;
        }

        var grouped = new Dictionary<string, string>(customFields.Count, StringComparer.Ordinal);
        foreach (CustomField customField in customFields)
        {
            grouped[customField.Key] = customField.Value;
        }

        return grouped;
    }
}
