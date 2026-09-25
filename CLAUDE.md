@.specify/memory/constitution.md

# NetPace Development Guide

## Quick Reference

This guide covers **HOW** (NetPace patterns, conventions, gotchas). The constitution covers **WHAT** and **WHY** (principles, governance).

## Project Overview

NetPace is a cross-platform network speed testing CLI built with .NET 10.0, using Ookla's Speedtest servers.

**Key Components:**
- `NetPace.Console` — CLI app using Spectre.Console
- `NetPace.Core` — Reusable library with `ISpeedTestService` interface (published to NuGet)

**Stack:** .NET 10.0 · C# 14 · Spectre.Console · System.CommandLine · xUnit · Nullable reference types enabled

## Testing

Test file mirrors source (`OoklaSpeedtest.cs` → `OoklaSpeedtestTests.cs`).

**Test in NetPace.Core:** all public APIs; speed calculations, unit conversions, server selection; happy paths, alternative configurations, and error scenarios (invalid input, network failures, timeouts).

**Console output:** `await Verify(result.Output)`; snapshots live in `NetPace.Console.Tests/Expectations/*.verified.txt`. A test with no snapshot fails first — review the generated `.received.txt`, then rename it to `.verified.txt` to accept. Constitution → Testing Standards for when a targeted assert is right instead.

## NetPace-Specific Patterns

### Speed Test Provider Pattern

Providers implement `ISpeedTestService` (`src/NetPace.Core/ISpeedTestService.cs`): server discovery, latency, download and upload. Each returns a result record (`LatencyTestResult`, `SpeedTestResult`) and has `CancellationToken` and `IProgress<T>` overloads. Provider code stays in `src/NetPace.Core/Clients/{Provider}/` (today only `Clients/Ookla/`).

### CLI Help Behaviour

`--help` (and `-h`, `-?`) is intercepted manually in `Program.RunAsync` before `System.CommandLine` parses arguments. This keeps help rendering under full control of `CustomHelpProvider`.

**Intentional constraint**: help is only recognised at position 0 (root help) or as the second token after a subcommand name (e.g. `netpace servers --help`). Flags placed before `--help` (e.g. `netpace --csv --help`) are silently ignored. Deliberate trade-off to keep the help-interception logic simple.

Do not add tests for the `--flag --help` pattern — it is not expected to work.

### Units and Formatting

`--unit-scale` overrides auto-scaling; formatting must match across normal, CSV and JSON output.

### Result, Extension and Settings Patterns

- Results are immutable records of raw measurements (`SpeedTestResult { BytesProcessed, ElapsedMilliseconds, RequestsSucceeded, RequestsFailed }`). Formatting lives in extensions, not on the record:
  ```csharp
  // src/NetPace.Core/SpeedTestExtensions.cs
  public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
  ```
- Provider configuration is a settings record passed to the constructor, not per-call parameters: `new OoklaSpeedtest(new OoklaSpeedtestSettings(Profile.Small))`. Override single fields with `with`.

## Working with Claude Code

Paired rules — `Don't` X → `Do` Y instead:

- **Constitution rules apply as written** → TDD, including the config/tooling carve-out where the real tool is the RED test (I); XML docs on public Core APIs (V); justified Core dependencies (VI); discuss public-API changes before implementing (VII); no skipped tests, enforced by the no-skipped-tests hook (X).
- **Don't commit with failing tests or build warnings** → run `dotnet build src` and `dotnet test src` clean before committing.
- **Don't change a CLI option without updating user-facing docs** → README.md `--help` snapshot and USER_GUIDE.md need updating; design-doc cross-ref where applicable. (Per-release "what changed" notes are GitHub-auto-generated from merged PRs — no CHANGELOG.md to maintain.)
- **Don't change `release-binaries.yml` (or other release-pipeline scope) without updating `docs/RELEASING.md`** → the release matrix, runner-per-RID rationale, naming convention, smoke-test contract, and size-assertion contract live there. Out-of-sync release docs make adding a new RID/variant cost extra. (Action-version bumps such as `actions/checkout@v4→v5` are exempt.)
- **Don't introduce reflection-heavy or non-trim-safe code** → NetPace targets AOT-trimmable builds (Spectre.Console.Cli was replaced for this reason); avoid runtime reflection, keep types annotation-clean.
- **Don't hard-wrap markdown prose** → write one line per paragraph, bullet, and table row and let the viewer soft-wrap it; a fixed-column hard wrap reflows the whole block on a one-word edit and buries the real change in a noisy diff.
- **Don't frame a decision in implementation jargon** → when putting a choice to Frank (an `AskUserQuestion`, a spec tradeoff), lead with plain-language consequences — what it costs, what it unlocks — before the mechanism.
- **Don't fold a second, unrelated mission into an in-flight branch** → ship the original branch with a documented known-issue and open a separate branch/issue for the new mission instead.

## Quick Command Reference

```bash
dotnet build src
dotnet test src
dotnet test src --collect:"XPlat Code Coverage"
```

## Detailed References

Load these on demand for the matching topic:

- **C# Style** — `docs/conventions/csharp-style.md` — read before writing or reviewing any .cs file.
- **Change Intent Records** — `docs/conventions/change-intent-records.md` — read when deciding whether a change warrants documenting intent.
- **Release Pipeline** — `docs/RELEASING.md` — release matrix (RIDs × variants), naming convention, runner-per-RID rationale, smoke-test contract, size-assertion contract. Update whenever you touch `release-binaries.yml`.
- **Ookla Download/Upload Sizing** — `docs/architecture/download-upload-size-controls.md` — how `OoklaSpeedtestSettings` shapes per-request sizing, iterations, and parallelism; what `--downloadsize`/`--uploadsize` actually cap (total-byte budget); Docker OoklaServer endpoints for local verification.

---

**Last Updated**: April 2026 · **Maintained by**: Frank Ray · **Constitution**: `.specify/memory/constitution.md`

---

## Project Memory

Project memory lives in [.claude/memory/](.claude/memory/) — git-tracked, written here (not user-level) so it travels with the codebase.

@.claude/memory/MEMORY.md
