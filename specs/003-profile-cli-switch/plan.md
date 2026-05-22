# Implementation Plan: Add `--profile` CLI switch (Tiny/Small/Medium/Large/Mega)

**Branch**: `003-profile-cli-switch` | **Date**: 2026-05-15 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-profile-cli-switch/spec.md`

## Summary

Introduce a public, provider-agnostic `Profile` enum in `NetPace.Core` with five members (`Tiny`, `Small`, `Medium`, `Large`, `Mega`) and wire it through two new public constructors on `OoklaSpeedtestSettings` (parameterless → Medium; `Profile`-taking with an inline switch holding the entire profile → settings mapping). Move the existing `DownloadSizeMb` / `UploadSizeMb` total-byte-budget caps off `ISpeedTestService` method overloads (deleting them as a breaking change) and onto the corresponding per-phase settings records, so a single `--profile` CLI flag coherently bundles per-request shape (`DownloadSizes`, iterations, parallel tasks) and the cap. `Medium` becomes the new default; explicit `--downloadsize` / `--uploadsize` still override only the cap via `with`-expressions.

Approach: edit existing files (no new project, no new abstraction layer). One inline switch in one constructor is the single source of truth for the profile → Ookla mapping; `Profile` itself stays a pure label with no provider knowledge so a second provider's settings record can supply its own translation later. Tests follow TDD per the constitution — RED-GREEN-REFACTOR on each new public surface — written into the existing `NetPace.Core.Tests` and `NetPace.Console.Tests` projects.

## Technical Context

**Language/Version**: C# 12 · .NET 8.0 (cross-platform)
**Primary Dependencies**: Spectre.Console (console UI), System.CommandLine (CLI binding), xUnit (tests), VerifyXunit (snapshot tests). No new dependencies introduced by this feature.
**Storage**: N/A — no persisted state; settings are constructed in-memory per-run.
**Testing**: xUnit · VerifyXunit (existing snapshot-test pattern under `NetPace.Console.Tests/Expectations/*.verified.txt`). Test conventions: file mirrors source (`OoklaSpeedtestSettings.cs` → `OoklaSpeedtestSettingsTests.cs`), partial-class split where appropriate (e.g. `NetPaceConsoleTests.Default.cs`), GIVEN-WHEN-THEN names (`MethodName_Scenario_ExpectedResult`).
**Target Platform**: Windows, Linux, macOS (.NET 8.0 cross-platform); no platform-specific code paths in scope.
**Project Type**: Library + CLI — `NetPace.Core` (NuGet-published library) and `NetPace.Console` (CLI consumer).
**Performance Goals**: Per-run transferred bytes must fall within ±10 % of each profile's published target (Tiny ~245 KB / Small ~10 MiB / Medium ~100 MiB / Large ~1 GiB / Mega ~10 GiB total down + up). Default-profile traffic must drop ≥ 65 % vs the prior ~370 MiB baseline (SC-002).
**Constraints**: AOT-trimmable — no reflection-heavy code; no runtime type discovery; pure constructor / switch-expression dispatch only. Public API additions require XML docs (constitution V). NetPace is pre-1.0 so breaking `ISpeedTestService` overload deletions are acceptable but must be flagged in PR title for auto-generated release notes.
**Scale/Scope**: One enum, two new constructors, two property moves (DownloadSizeMb / UploadSizeMb into per-phase records), one new CLI option, six method-overload deletions on `ISpeedTestService` (and matching `OoklaSpeedtest`), plus docs (README, USER_GUIDE, architecture, CIR).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Constitution principles evaluated against this plan:

| Principle | Check | Verdict |
|---|---|---|
| **I — TDD (non-negotiable)** | Every new public surface (Profile enum, both ctors, relocated properties, CLI flag) is added test-first. Tests already enumerated in spec FR-005..FR-007 and SC-005. | PASS |
| **II — Library-First** | `Profile` and the ctor-driven mapping live in `NetPace.Core`. CLI is a thin consumer that parses `--profile` and threads the value into a `new OoklaSpeedtestSettings(profile)` call. Library is independently usable by NuGet consumers (US-5). | PASS |
| **III — CLI Excellence** | `--profile` follows the established enum-flag pattern (`--unit-system`, `--unit-scale`). Default is sensible (`Medium`). `--help` shows the full enum value list. Output formats unaffected. | PASS |
| **IV — Cross-Platform** | No platform-specific APIs; no filesystem or process-level dependencies introduced. | PASS |
| **V — Code Quality (naming, XML docs, async/CT, nullable, no warnings)** | XML docs on every new public member (FR-021). Naming follows PascalCase enum convention. No new async surface; existing CT contracts preserved. Nullable already enabled project-wide. | PASS |
| **VI — Minimal Dependencies** | No new NuGet packages. All work is internal type/method additions on existing assemblies. | PASS |
| **VII — Semantic Versioning** | Breaking change: `ISpeedTestService` overload deletions and new default profile. Per pre-1.0 policy + spec Assumptions, flagged in PR title; auto-generated release notes pick up the breaking-change marker. | PASS (documented) |
| **VIII — AC-to-Test traceability** | Spec acceptance scenarios already carry `**Scenario:**` labels; test-plan generation will match them verbatim. | PASS |

**Gate result: PASS.** No constitutional violations. Complexity Tracking section omitted (no violations to justify).

## Project Structure

### Documentation (this feature)

```text
specs/003-profile-cli-switch/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── profile-enum.md           # Public Profile enum contract
│   ├── ooklasettings-ctors.md    # OoklaSpeedtestSettings ctor contract
│   ├── speedtestservice-surface.md  # ISpeedTestService overload deletions
│   └── cli-flag.md               # --profile CLI binding contract
├── checklists/
│   └── requirements.md  # Created by /speckit.specify
└── tasks.md             # Created by /speckit.tasks (not by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── NetPace.Core/
│   ├── Profile.cs                                ◄ NEW (public enum, sibling of SpeedUnit*)
│   ├── ISpeedTestService.cs                      ◄ EDIT (delete int sizeMb overloads — 4 methods)
│   ├── SpeedUnit.cs / SpeedScale.cs / SpeedUnitSystem.cs   (pattern reference; unchanged)
│   └── Clients/Ookla/
│       ├── OoklaSpeedtestSettings.cs             ◄ EDIT (two new public ctors + inline switch)
│       ├── OoklaSpeedtest.cs                     ◄ EDIT (read DownloadSizeMb/UploadSizeMb off settings; remove method overloads)
│       └── Settings/
│           ├── DownloadTestSettings.cs           ◄ EDIT (add DownloadSizeMb property; default int.MaxValue)
│           └── UploadTestSettings.cs             ◄ EDIT (add UploadSizeMb property; default int.MaxValue)
├── NetPace.Console/
│   ├── Program.cs                                ◄ EDIT (add --profile Option<Profile>, wire through; rewire call sites at L232-233)
│   └── Commands/
│       └── SpeedTestCommandSettings.cs           ◄ EDIT (Profile property)
└── NetPace.Core.Tests/
    ├── ProfileTests.cs                           ◄ NEW (enum-level structural tests; FR-001..FR-002)
    └── OoklaSpeedtestSettingsTests.cs            ◄ NEW (ctor mapping per profile; FR-003..FR-008)
    └── OoklaSpeedtestSettingsTests.Profiles.cs   ◄ NEW (partial — per-profile exact-equality assertions; SC-005)
src/NetPace.Console.Tests/
    ├── NetPaceConsoleTests.Profile.cs            ◄ NEW (--profile binding, defaults, override interaction; FR-012..FR-017)
    └── Expectations/                             ◄ EDIT (refresh --help and any output snapshots affected)

docs/
├── architecture/download-upload-size-controls.md ◄ EDIT (cross-ref profiles → per-request tables; Mega warning)
└── change-intent-records/
    └── CIR-NNN-profile-cli-switch.md             ◄ NEW (public-API addition record; per FR-025)

README.md                                          ◄ EDIT (refresh --help snapshot; --profile in options table)
USER_GUIDE.md                                      ◄ EDIT (new "Choosing a profile" section with table; Mega warning callout)
```

**Structure Decision**: Single existing solution layout retained. No new project. All work lands as edits in `src/NetPace.Core/`, `src/NetPace.Console/`, and the two existing test projects, plus the three docs. The `Profile.cs` placement at the **top level** of `NetPace.Core` (sibling of `SpeedUnit.cs`) is load-bearing: it enforces FR-001 (top-level) and FR-002 (no provider import) by file location alone, making the "provider knows `Profile`; `Profile` knows no provider" rule grep-able and structurally enforced.

## Complexity Tracking

No constitutional violations. Section intentionally empty.
