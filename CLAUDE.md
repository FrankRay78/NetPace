@CONVENTIONS.md

# NetPace Development Guide

## Quick Reference

This guide covers NetPace's product principles, patterns and gotchas. `CONVENTIONS.md`, imported above, holds the rules of the codebase and what the harness commands apply that is particular to NetPace, so every session loads it with this file; `REVIEW.md` holds what the pull request review checks. Each rule has one home among the three, and none overrides another. A `Read` of this file does not follow the import; open `CONVENTIONS.md` itself when a command names a section of it.

## Project Overview

NetPace is a cross-platform network speed testing CLI built with .NET 10.0, using Ookla's Speedtest servers.

**Key Components:**
- `NetPace.Console` — CLI app using Spectre.Console
- `NetPace.Core` — Reusable library with `ISpeedTestService` interface (published to NuGet)

**Stack:** .NET 10.0 · C# 14 · Spectre.Console · System.CommandLine · xUnit · Nullable reference types enabled

## Product Principles

- **Library-first** — every feature starts as part of the `NetPace.Core` library before any CLI implementation. The library is self-contained, independently testable and documented; it has no dependency on the Console application and is usable in any context — console, web API, GUI, tests. A library has a clear purpose, never an organisational one, and interfaces are preferred over concrete implementations for abstraction and testability. Consumers can use `NetPace.Core` from NuGet without any CLI dependency.
- **CLI excellence** — the command line follows the [CLI Guidelines (clig.dev)](https://clig.dev/) strictly, with one exception while NetPace is pre-1.0 (`CONVENTIONS.md` → *Public API and breaking changes*): clig.dev's advice to warn users before changing or removing an option does not apply, because there is no deprecation period. A removed or renamed option should fail with a message saying it was removed, where that is simple to do — a preference, never a requirement — and the exception lapses when 1.0.0 is tagged. All console output and interaction goes through Spectre.Console; `--help` and `--version` are supported; error messages are clear and give actionable guidance; the default behaviour works for most users without flags.
- **Default output is for a person at a modern terminal.** It may use animation, colour, Unicode and live redraw, and is not designed for backward compatibility or scraping: its layout may change in any release. Where it cannot animate, including pipes, redirects, `--file` and terminals that cannot redraw, it writes the `--simple` output instead. It stays readable without colour, under `NO_COLOR` and the `plain` theme. `--simple`, `--csv` and `--json` are the stable formats for scripts. This rule governs how default output behaves, not how much it is prioritised. Rationale: CIR [`2026-11-09-default-output-is-for-a-modern-terminal`](docs/change-intent-records/2026-11-09-default-output-is-for-a-modern-terminal.md).
- **Output formats** — default, simple, CSV and JSON, for scripting as well as reading. Each format is selected by one switch of its own, and exactly one may be selected: a format selector is not a level on a scale, and the two are never conflated in a single option. What the formats carry is under *Units and Formatting* below.
- **Cross-platform** — NetPace runs on Windows, Linux and macOS; the codebase rule is under `CONVENTIONS.md` → *Code quality*.
- **Versioning** — releases follow semantic versioning, with a pre-1.0 stance that lifts the breaking-change obligations until 1.0.0 is tagged: `CONVENTIONS.md` → *Public API and breaking changes*.

## NetPace-Specific Patterns

### Speed Test Provider Pattern

Providers implement `ISpeedTestService` (`src/NetPace.Core/ISpeedTestService.cs`): server discovery, latency, download and upload. Each returns a result record (`LatencyTestResult`, `SpeedTestResult`) and has `CancellationToken` and `IProgress<T>` overloads. Provider code stays in `src/NetPace.Core/Clients/{Provider}/` (today only `Clients/Ookla/`).

### CLI Help Behaviour

`--help` (and `-h`, `-?`) is intercepted manually in `Program.RunAsync` before `System.CommandLine` parses arguments. This keeps help rendering under full control of `CustomHelpProvider`.

**Intentional constraint**: help is only recognised at position 0 (root help) or as the second token after a subcommand name (e.g. `netpace servers --help`). Flags placed before `--help` (e.g. `netpace --csv --help`) are silently ignored. Deliberate trade-off to keep the help-interception logic simple.

### Units and Formatting

Speeds support SI (1000-based) and IEC (1024-based) unit systems, in bits or bytes per second, auto-scaled by default (Mbps, Gbps). `--unit-scale` overrides auto-scaling; formatting must match across default and simple output. Simple output writes one `name: value` line per figure (`Timestamp`, `Ping`, `Download`, `Upload`), where default output lays the figures out as a table, one row per measurement, and names them for a person reading columns (`Pinging`, `Latency`, `Jitter`) — so the figures agree while the shape and the labels do not. Simple output is also what default output writes wherever it cannot animate (*Product Principles*), and there the two are the same lines, not two renderings of them. CSV and JSON are not formatted output — both carry plain numbers in fixed units (`LatencyMilliseconds`, `…SpeedBitsPerSecond`) that the unit options do not touch, and the numbers they report must agree with each other. The two formats also carry the same fields under the same names, so a new field goes into both or neither; only the shape for something missing differs, CSV always carrying every built-in column and leaving the cell empty where JSON leaves the field out. User-defined `--custom-field` pairs are the one thing outside that rule in shape but not in name: trailing columns in CSV, one grouped `CustomFields` object in JSON, so a custom key can never displace a built-in field. Rationale: CIR [`2026-10-30-csv-and-json-carry-the-same-fields`](docs/change-intent-records/2026-10-30-csv-and-json-carry-the-same-fields.md).

### Result, Extension and Settings Patterns

- Results are immutable records of raw measurements (`SpeedTestResult { BytesProcessed, ElapsedMilliseconds, RequestsSucceeded, RequestsFailed }`). Formatting lives in extensions, not on the record:
  ```csharp
  // src/NetPace.Core/SpeedTestExtensions.cs
  public static string GetSpeedString(this SpeedTestResult result, SpeedUnit unit, SpeedUnitSystem unitSystem, SpeedScale scale = SpeedScale.Auto)
  ```
- Provider configuration is a settings record passed to the constructor, not per-call parameters: `new OoklaSpeedtest(new OoklaSpeedtestSettings(Profile.Small))`. Override single fields with `with`.

## Detailed References

Load these on demand for the matching topic:

- **Conventions** — `CONVENTIONS.md` — already loaded by the import above: the rules of the codebase (TDD, testing, acceptance criteria, code quality, dependencies, versioning, commits) and the build, test, format, documentation and issue bindings the commands read, with the hooks and plugins a port needs. Edit it, not a gaffer command, to change how NetPace builds, tests, documents or raises work.
- **Review criteria** — `REVIEW.md` — what the PR review applies that is particular to NetPace: severities, high-risk areas, always-a-person paths, contracts. Edit it, not gaffer's `review-pr` command, to change what the review holds NetPace to.
- **C# Style** — `docs/conventions/csharp-style.md` — read before writing or reviewing any .cs file.
- **Testing** — `docs/conventions/testing.md` — read before writing tests: outcome-not-mechanism assertions, what makes a good scenario, the integrity failure modes that pass while verifying nothing.
- **Change Intent Records** — `docs/conventions/change-intent-records.md` — read when deciding whether a change warrants documenting intent.
- **Release Pipeline** — `docs/RELEASING.md` — release matrix (RIDs × variants), naming convention, runner-per-RID rationale, smoke-test contract, size-assertion contract.
- **Server Screening and Measurement** — `docs/architecture/server-screening-and-measurement.md` — read before changing server selection, the latency probe, or the settings that bound either; records why choosing and measuring are separate jobs and why the ceiling on choosing must stay.
- **Ookla Download/Upload Sizing** — `docs/architecture/download-upload-size-controls.md` — how `OoklaSpeedtestSettings` shapes per-request sizing, iterations, and parallelism; what `--downloadsize`/`--uploadsize` actually cap (total-byte budget); Docker OoklaServer endpoints for local verification.

---

**Last Updated**: November 2026 · **Maintained by**: Frank Ray · **Rules**: `CONVENTIONS.md` · **Review criteria**: `REVIEW.md`

---

## Project Memory

Project memory lives in [.claude/memory/](.claude/memory/) — git-tracked, written here (not user-level) so it travels with the codebase.

@.claude/memory/MEMORY.md
