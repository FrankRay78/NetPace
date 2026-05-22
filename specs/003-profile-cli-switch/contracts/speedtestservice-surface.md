# Contract — `ISpeedTestService` surface change

**Namespace**: `NetPace.Core`
**File**: `src/NetPace.Core/ISpeedTestService.cs`
**Stability**: pre-1.0 — **breaking change** to the public NuGet contract. Flagged in PR title so GitHub auto-generated release notes pick it up.

## Methods REMOVED (breaking)

```csharp
// All four overloads below are DELETED.

Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken cancellationToken = default);
Task<SpeedTestResult> GetDownloadSpeedAsync(IServer server, int downloadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken cancellationToken = default);
Task<SpeedTestResult> GetUploadSpeedAsync(IServer server, int uploadSizeMb, IProgress<SpeedTestProgress> progress, CancellationToken cancellationToken = default);
```

## Methods PRESERVED

```csharp
Task<IServer[]>          GetServersAsync(CancellationToken ct = default);

Task<LatencyTestResult>  GetServerLatencyAsync(IServer server, CancellationToken ct = default);
Task<LatencyTestResult>  GetServerLatencyAsync(IServer server, IProgress<LatencyTestProgress> progress, CancellationToken ct = default);
Task<LatencyTestResult>  GetServerLatencyAsync(string serverUrl, CancellationToken ct = default);
Task<LatencyTestResult>  GetServerLatencyAsync(string serverUrl, IProgress<LatencyTestProgress> progress, CancellationToken ct = default);

Task<LatencyTestResult>  GetFastestServerByLatencyAsync(IServer[] servers, CancellationToken ct = default);
Task<LatencyTestResult>  GetFastestServerByLatencyAsync(IServer[] servers, IProgress<SpeedTestProgress> progress, CancellationToken ct = default);

Task<SpeedTestResult>    GetDownloadSpeedAsync(IServer server, CancellationToken ct = default);
Task<SpeedTestResult>    GetDownloadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken ct = default);

Task<SpeedTestResult>    GetUploadSpeedAsync(IServer server, CancellationToken ct = default);
Task<SpeedTestResult>    GetUploadSpeedAsync(IServer server, IProgress<SpeedTestProgress> progress, CancellationToken ct = default);
```

## Behavioural contract

| ID | Behaviour |
|---|---|
| **C-SS-1** | Total-byte budget cap is read from the settings record (`DownloadTestSettings.DownloadSizeMb` / `UploadTestSettings.UploadSizeMb`) at speed-test construction, not passed per-call. |
| **C-SS-2** | Per-call cap variation uses `settings with { DownloadTest = settings.DownloadTest with { DownloadSizeMb = N } }`, then a new `OoklaSpeedtest` instance — there is no longer a "vary the cap on this one call" overload. |
| **C-SS-3** | XML docs on surviving methods do not mention `int sizeMb`. |

## Migration note for NuGet consumers (for release notes)

Before:
```csharp
var result = await service.GetDownloadSpeedAsync(server, downloadSizeMb: 100, ct);
```

After:
```csharp
var settings = new OoklaSpeedtestSettings(Profile.Medium) with
{
    DownloadTest = new DownloadTestSettings { /* fields */, DownloadSizeMb = 100 }
};
var service = new OoklaSpeedtest(settings);
var result = await service.GetDownloadSpeedAsync(server, ct);
```

The conceptual replacement is "configure once, run normally" instead of "configure per-call". This matches the rest of the settings surface (server discovery, latency, proxy) which has never had per-call overloads.
