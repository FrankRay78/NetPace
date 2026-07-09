# Phase 1 — Data Model: `--profile` CLI switch

**Feature**: 003-profile-cli-switch
**Date**: 2026-05-15

This feature changes type-level state on five existing types and adds one new enum. There is no persisted data and no state machine — all entities are immutable record / enum types built per-run.

---

## Entities

### `Profile` *(NEW · public enum · `NetPace.Core`)*

**File**: `src/NetPace.Core/Profile.cs`
**Visibility**: `public`
**Kind**: enum (default `int` backing)
**Provider-coupled**: NO (validated by file location and grep — FR-001/FR-002)

| Member | Ordinal | Intent |
|---|---|---|
| `Tiny`   | 0 | IoT / 10 MB-month plans |
| `Small`  | 1 | Cellular / metered |
| `Medium` | 2 | Typical home broadband (default) |
| `Large`  | 3 | Fibre / business |
| `Mega`   | 4 | Inter-DC / 10 Gbps saturation |

Ordering is **ascending traffic-load** so future code that wants to compare "heaviness" can cast to `int` if needed (not done by this feature).

**XML doc requirements**: every member documented. `Mega`'s doc must explicitly call out the undocumented-payload dependency and cross-reference `docs/architecture/download-upload-size-controls.md` (FR-021).

**Validation rules**: no runtime validation on the enum itself; the receiving constructor (`OoklaSpeedtestSettings(Profile)`) throws `ArgumentOutOfRangeException` on unknown values (FR-007).

---

### `OoklaSpeedtestSettings` *(EDIT · public sealed record · `NetPace.Core.Clients.Ookla`)*

**File**: `src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs`
**State change**: no new fields; instance state stays pure data (FR-008 — no `Profile` property).

**Constructors added** (both public, both XML-documented):

| Signature | Behaviour |
|---|---|
| `OoklaSpeedtestSettings()` | Chains via `: this(Profile.Medium)`. Single source of truth for the default (FR-004). |
| `OoklaSpeedtestSettings(Profile profile)` | Inline `switch` expression that populates `DownloadTest` and `UploadTest` for the chosen profile. Throws `ArgumentOutOfRangeException(nameof(profile))` for unknown values (FR-007). |

**Removed initializer**: `DownloadTest` and `UploadTest` lose their property initializers (`= new();`) because the constructor body now assigns them.

**Existing fields unchanged**: `ServerDiscovery`, `LatencyTest`, `ProxyCredential`, `ProxyAddress`, `UseProxy`.

**`with`-expression compatibility**: synthesised record copy-constructor is unaffected by user-defined constructors — `new OoklaSpeedtestSettings(Profile.Mega) with { UseProxy = true }` continues to work (FR — implicit, exercised by US-5 acceptance).

---

### `DownloadTestSettings` *(EDIT · public sealed record · `NetPace.Core.Clients.Ookla.Settings`)*

**File**: `src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs`

**New property**:

| Property | Type | Default | XML `<remarks>` |
|---|---|---|---|
| `DownloadSizeMb` | `int` | `int.MaxValue` | Disambiguates from `DownloadSizes` (the per-request pixel array): this is the total-byte budget cap in IEC MiB. |

**Existing properties unchanged**: `DownloadSizes`, `DownloadSizeIterations`, `DownloadParallelTasks`.

**Per-profile values populated by `OoklaSpeedtestSettings(Profile)`**:

| Profile | `DownloadSizes` | `DownloadSizeIterations` | `DownloadParallelTasks` | `DownloadSizeMb` |
|---|---|---|---|---|
| Tiny   | `[350]`                              | 1  | 1  | 1     |
| Small  | `[1000, 1500]`                       | 2  | 2  | 10    |
| Medium | `[1500, 2000, 3000, 3500, 4000]`     | 2  | 4  | 100   |
| Large  | `[2000, 2500, 3000, 3500, 4000]`     | 12 | 16 | 1024  |
| Mega   | `[3000, 4000, 5000, 6000, 7000]`     | 40 | 32 | 10240 |

---

### `UploadTestSettings` *(EDIT · public sealed record · `NetPace.Core.Clients.Ookla.Settings`)*

**File**: `src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs`

**New property**:

| Property | Type | Default | XML `<remarks>` |
|---|---|---|---|
| `UploadSizeMb` | `int` | `int.MaxValue` | Total-byte budget cap in IEC MiB. Distinct from the per-request size derived from `UploadSizeIncrementKb` × `UploadIncrements`. |

**Existing properties unchanged**: `UploadSizeIncrementKb`, `UploadIncrements`, `UploadSizeIterations`, `UploadParallelTasks`.

**Per-profile values populated by `OoklaSpeedtestSettings(Profile)`**:

| Profile | `UploadSizeIncrementKb` | `UploadIncrements` | `UploadSizeIterations` | `UploadParallelTasks` | `UploadSizeMb` |
|---|---|---|---|---|---|
| Tiny   | 50   | 1  | 1  | 1  | 1    |
| Small  | 100  | 4  | 2  | 2  | 2    |
| Medium | 200  | 6  | 5  | 4  | 25   |
| Large  | 500  | 8  | 12 | 16 | 256  |
| Mega   | 1024 | 16 | 16 | 32 | 2048 |

---

### `ISpeedTestService` *(EDIT · public interface · `NetPace.Core`)*

**File**: `src/NetPace.Core/ISpeedTestService.cs`

**Breaking change — methods REMOVED** (D3/FR-010):

- `GetDownloadSpeedAsync(IServer server, int downloadSizeMb, CancellationToken)`
- `GetDownloadSpeedAsync(IServer server, int downloadSizeMb, IProgress<SpeedTestProgress>, CancellationToken)`
- `GetUploadSpeedAsync(IServer server, int uploadSizeMb, CancellationToken)`
- `GetUploadSpeedAsync(IServer server, int uploadSizeMb, IProgress<SpeedTestProgress>, CancellationToken)`

**Surviving signatures per direction**:

- `Task<SpeedTestResult> GetDownloadSpeedAsync(IServer, CancellationToken)`
- `Task<SpeedTestResult> GetDownloadSpeedAsync(IServer, IProgress<SpeedTestProgress>, CancellationToken)`
- `Task<SpeedTestResult> GetUploadSpeedAsync(IServer, CancellationToken)`
- `Task<SpeedTestResult> GetUploadSpeedAsync(IServer, IProgress<SpeedTestProgress>, CancellationToken)`

The byte-cap is now read from `DownloadTestSettings.DownloadSizeMb` / `UploadTestSettings.UploadSizeMb` on the settings record set at `OoklaSpeedtest` construction.

---

### `OoklaSpeedtest` *(EDIT · public class · `NetPace.Core.Clients.Ookla`)*

**File**: `src/NetPace.Core/Clients/Ookla/OoklaSpeedtest.cs`

- Matching overload removals (mirror of the `ISpeedTestService` deletions).
- The internal `GenericTestSpeedAsync` / equivalent loop reads its `maxBytes` cap from `settings.DownloadTest.DownloadSizeMb` (resp. `UploadSizeMb`) at call-time, converted to bytes (× 1024 × 1024 for IEC MiB).
- XML docs updated on surviving methods to remove obsolete references to the `int sizeMb` parameter.

---

### `SpeedTestCommandSettings` *(EDIT · CLI command-settings class · `NetPace.Console`)*

**File**: `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`

- New property: `public Profile Profile { get; init; } = Profile.Medium;` (kept aligned with the CLI flag default).

### `Program.cs` *(EDIT · `NetPace.Console`)*

**File**: `src/NetPace.Console/Program.cs` (call sites at L232-233 per issue body)

- New `Option<Profile> profileOption` declared next to the existing `--unit-system` option.
- Bound onto `SpeedTestCommandSettings.Profile`.
- The construction of `OoklaSpeedtestSettings` is rewired from "parameterless + with-override of `DownloadTest`/`UploadTest`" to `new OoklaSpeedtestSettings(settings.Profile) with { … }`, where the `with` block carries only:
  - Explicit `--downloadsize` → `DownloadTest = previousDownloadTest with { DownloadSizeMb = N }`.
  - Explicit `--uploadsize` → `UploadTest = previousUploadTest with { UploadSizeMb = N }`.
  - Proxy fields unchanged.

---

## Relationships

```
Profile (enum)  ──read by──►  OoklaSpeedtestSettings(Profile) ctor
                                  │
                                  ▼
                              { DownloadTest, UploadTest }  ◄── DownloadTestSettings / UploadTestSettings
                                  │                                  │
                                  │                                  └─ DownloadSizeMb / UploadSizeMb
                                  ▼
                              OoklaSpeedtest  ──reads──►  settings.DownloadTest.DownloadSizeMb
                                                          settings.UploadTest.UploadSizeMb
```

**Dependency direction**: arrows only flow inward into provider-specific code. `Profile` (top-level `NetPace.Core`) has zero references to anything under `Clients/`. `OoklaSpeedtestSettings` knows `Profile`; the reverse never holds.

## State transitions

None. All types are immutable records / enums. Settings are constructed once per run and never mutated; `with`-expressions produce new instances.

## Validation rules

| Where | Rule | Mechanism |
|---|---|---|
| `OoklaSpeedtestSettings(Profile)` | Unknown `Profile` value rejected | Switch-expression default arm throws `ArgumentOutOfRangeException(nameof(profile))` (FR-007). |
| `--profile` CLI binding | Unknown string rejected | `System.CommandLine` default enum-binding error (FR-013). |
| `--profile` case-insensitive parse | `tiny`, `Tiny`, `TINY` all parse | Existing `System.CommandLine` enum-binding behaviour (FR-012). |
| `DownloadSizeMb` / `UploadSizeMb` raw-record default | Sentinel `int.MaxValue` = "no cap" | Cap-check loop in `OoklaSpeedtest` never trips when cap exceeds natural transfer (FR-011, FR-017). |
