# NetPace Development Guide

## Quick Reference

**For Core Principles & Governance**: See `.specify/memory/constitution.md`

This guide covers **HOW** (NetPace patterns, conventions, gotchas). The constitution covers **WHAT** and **WHY** (principles, governance). Generic C# style lives in `docs/conventions/csharp-style.md` — read that for naming, async, LINQ, member ordering, etc.

## Project Overview

NetPace is a cross-platform network speed testing CLI built with .NET 8.0, using Ookla's Speedtest servers.

**Key Components:**
- `NetPace.Console` — CLI app using Spectre.Console
- `NetPace.Core` — Reusable library with `ISpeedTestService` interface (published to NuGet)

**Stack:** .NET 8.0 · C# 12 · Spectre.Console · System.CommandLine · xUnit · Nullable reference types enabled

## Testing

Project layout: `NetPace.Core.Tests`, `NetPace.Console.Tests`. Test file mirrors source (`OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`). xUnit, Given-When-Then, naming `MethodName_Scenario_ExpectedResult`.

**Test in NetPace.Core:** all public APIs; speed calculations, unit conversions, server selection; happy paths, alternative configurations, and error scenarios (invalid input, network failures, timeouts). Real-network integration tests live in a separate test category.

**Don't test:** Spectre.Console output (trust the library); simple getters/setters with no logic; third-party library behaviour.

## NetPace-Specific Patterns

### Speed Test Provider Pattern

All speed test implementations must implement `ISpeedTestService`:

```csharp
public interface ISpeedTestService
{
    Task<IEnumerable<Server>> GetServersAsync(...);
    Task<LatencyResult> GetLatencyAsync(Server server, ...);
    Task<DownloadResult> GetDownloadSpeedAsync(Server server, ...);
    Task<UploadResult> GetUploadSpeedAsync(Server server, ...);
}
```

- Currently using Ookla; architecture allows alternatives
- Provider-specific code stays isolated in `Clients/{ProviderName}/`

### CLI Help Behaviour

`--help` (and `-h`, `-?`) is intercepted manually in `Program.RunAsync` before `System.CommandLine` parses arguments. This keeps help rendering under full control of `CustomHelpProvider`.

**Intentional constraint**: help is only recognised at position 0 (root help) or as the second token after a subcommand name (e.g. `netpace servers --help`). Flags placed before `--help` (e.g. `netpace --csv --help`) are silently ignored. Deliberate trade-off to keep the help-interception logic simple.

Do not add tests for the `--flag --help` pattern — it is not expected to work.

### Units and Formatting

- **Unit systems**: SI (1000-based: KB, MB, GB) and IEC (1024-based: KiB, MiB, GiB)
- **Speed units**: BitsPerSecond and BytesPerSecond
- **Scaling**: auto-scale by default (Mbps, Gbps); user override via `--unit-scale`
- **Consistency**: same formatting across normal, CSV, and JSON output

### Common Code Patterns

#### Result Objects

Return rich result objects with full test information:

```csharp
public class DownloadResult
{
    public double SpeedBitsPerSecond { get; init; }
    public TimeSpan Duration { get; init; }
    public long BytesTransferred { get; init; }

    public string GetSpeedString(SpeedUnit unit, SpeedUnitSystem system) { ... }
}
```

#### Extension Methods

Use extension methods for formatting and conversion logic that doesn't belong on the core type:

```csharp
public static class SpeedResultExtensions
{
    public static string GetSpeedString(this DownloadResult result, ...) { ... }
}
```

#### Options Pattern

For complex configuration, use options objects instead of many parameters:

```csharp
public async Task<DownloadResult> GetDownloadSpeedAsync(
    Server server,
    DownloadTestSettings? settings = null,
    CancellationToken cancellationToken = default)
{
    settings ??= DownloadTestSettings.Default;
    // ...
}
```

## Working with Claude Code

Paired rules — `Don't` X → `Do` Y instead:

- **Don't write production code without a failing test** → write a RED test first, watch it fail, *then* implement (RED-GREEN-REFACTOR; see constitution).
- **Don't change public APIs in `NetPace.Core` without discussion** → public-API changes affect NuGet consumers; raise the change for approval before implementing.
- **Don't ship a public `NetPace.Core` API without XML docs** → all public methods, properties, and classes in `NetPace.Core` need `///` XML docs (they ship to NuGet consumers).
- **Don't add a `NetPace.Core` dependency without justification** → keep the library lean; if a new dep is needed, justify it explicitly in the PR or CIR.
- **Don't commit with failing tests or build warnings** → run `dotnet build` and `dotnet test` clean before committing.
- **Don't change a CLI option without updating user-facing docs** → README.md `--help` snapshot and USER_GUIDE.md need updating; design-doc cross-ref where applicable. (Per-release "what changed" notes are GitHub-auto-generated from merged PRs — no CHANGELOG.md to maintain.)
- **Don't change `release-binaries.yml` (or other release-pipeline scope) without updating `docs/RELEASING.md`** → the release matrix, runner-per-RID rationale, naming convention, smoke-test contract, and size-assertion contract live there. Out-of-sync release docs make adding a new RID/variant cost extra.
- **Don't introduce reflection-heavy or non-trim-safe code** → NetPace targets AOT-trimmable builds (Spectre.Console.Cli was replaced for this reason); avoid runtime reflection, keep types annotation-clean.

## Quick Command Reference

```bash
# Build & test
dotnet build
dotnet test
dotnet test --collect:"XPlat Code Coverage"

# Start new work
git checkout main && git pull origin main
git checkout -b feature/your-feature-name
```

## Detailed References

Load these on demand for the matching topic:

- **C# Style** — `docs/conventions/csharp-style.md` — naming (`_camelCase`/`s_camelCase`/`t_camelCase`), `var` rules, async/`ConfigureAwait`, immutability, LINQ, member ordering, primary-constructor parameter casing, xUnit conventions.
- **Change Intent Records** — `docs/conventions/change-intent-records.md` — decision table for whether to write one, template, worked example.
- **Release Pipeline** — `docs/RELEASING.md` — release matrix (RIDs × variants), naming convention, runner-per-RID rationale, smoke-test contract, size-assertion contract. Update whenever you touch `release-binaries.yml`.
- **Ookla Download/Upload Sizing** — `docs/architecture/download-upload-size-controls.md` — how `OoklaSpeedtestSettings` shapes per-request sizing, iterations, and parallelism; what `--downloadsize`/`--uploadsize` actually cap (total-byte budget); Docker OoklaServer endpoints for local verification.

---

**Last Updated**: April 2026 · **Maintained by**: Frank Ray · **Constitution**: `.specify/memory/constitution.md`

---

## Project Memory

Feedback and project memory live in [.claude/memory/](.claude/memory/) — git-tracked, visible in diffs, travels with the codebase. Write new entries there; user-level memory is deprecated for this project.

@.claude/memory/MEMORY.md
