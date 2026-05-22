# Quickstart — `--profile` CLI switch

**Feature**: 003-profile-cli-switch
**Audience**: end users (CLI) and library consumers (NetPace.Core NuGet)

## CLI

```bash
# Default — Medium profile (~121 MiB total per run)
netpace

# IoT / 10 MB-month data plan (~0.3 MiB total per run; ≥ 30 runs/month within cap)
netpace --profile tiny

# Cellular / metered (~12 MiB total per run)
netpace --profile small

# Fibre / business (~1.2 GiB total per run)
netpace --profile large

# Inter-DC / 10 Gbps saturation (~12 GiB total per run; uses undocumented OoklaServer payloads)
netpace --profile mega

# Profile + override the cap only (Tiny's per-request shape, stop after 5 MiB downloaded)
netpace --profile tiny --downloadsize 5

# Profile + skip upload phase
netpace --profile large --no-upload
```

Case-insensitive: `--profile TINY`, `--profile Tiny`, and `--profile tiny` are all valid. Unknown values produce a `System.CommandLine` error.

## Library (NetPace.Core)

```csharp
using NetPace.Core;
using NetPace.Core.Clients.Ookla;

// Default — Medium
var defaultSettings = new OoklaSpeedtestSettings();

// Explicit profile
var tiny = new OoklaSpeedtestSettings(Profile.Tiny);

// Profile + non-payload customisation
var withProxy = new OoklaSpeedtestSettings(Profile.Mega) with
{
    UseProxy = true,
    ProxyAddress = new Uri("http://proxy.example.com:8080")
};

// Profile + cap override
var baseSettings = new OoklaSpeedtestSettings(Profile.Large);
var cappedAt100Mib = baseSettings with
{
    DownloadTest = baseSettings.DownloadTest with { DownloadSizeMb = 100 }
};

// Run
var service = new OoklaSpeedtest(cappedAt100Mib);
var result = await service.GetDownloadSpeedAsync(server, CancellationToken.None);
```

## Migration from prior `int sizeMb` overloads

If you previously called:

```csharp
await service.GetDownloadSpeedAsync(server, downloadSizeMb: 100, ct);
```

…replace with the settings-record approach:

```csharp
var settings = new OoklaSpeedtestSettings(Profile.Medium) with
{
    DownloadTest = /* …current DownloadTest… */ with { DownloadSizeMb = 100 }
};
var service = new OoklaSpeedtest(settings);
await service.GetDownloadSpeedAsync(server, ct);
```

The `int sizeMb` overloads on `ISpeedTestService` have been removed.

## Verifying

Quickest end-to-end check (uses the local Docker OoklaServer if you have one at `docker/ooklaserver/`, otherwise any public Ookla server):

```bash
dotnet build
dotnet test --filter "FullyQualifiedName~Profile"

# Tiny end-to-end (manual byte-budget check)
netpace --profile tiny --json
# Then inspect the run output's reported bytes and confirm ≤ 1 MiB total.
```

## Choosing a profile

| If you are… | Pick |
|---|---|
| Running on IoT / a 10 MB-month data plan | `Tiny` |
| On cellular / a metered link | `Small` |
| On typical home broadband | `Medium` *(default — omit `--profile`)* |
| On fibre / a business link | `Large` |
| Saturating a 10 Gbps inter-DC link | `Mega` *(warning: depends on undocumented OoklaServer payloads — may fail on future server versions)* |

The five values are deliberately coarse. If you need fine-grained per-knob control, use the library API directly and construct your own `OoklaSpeedtestSettings` record without going through `(Profile)`.
