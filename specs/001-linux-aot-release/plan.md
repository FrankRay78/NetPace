# Implementation Plan: Linux Native AOT Release Artifacts

**Branch**: `001-linux-aot-release` | **Date**: 2026-05-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-linux-aot-release/spec.md`

## Summary

Extend the existing release pipeline with two Native AOT archive variants for Linux IoT/embedded targets — `linux-x64-aot` and `linux-arm64-aot` — producing single, self-contained native ELF binaries that run without a .NET runtime. Cleanly remove the AOT-incompatible reflection paths in `NetPace.Core` (Ookla XML deserialization via `XmlSerializer`) and in `NetPace.Console` (`Humanizer`), and declare AOT compatibility on both projects so analyzer warnings (IL2026/IL2090/IL3050/IL3056) become build-time failures. Existing 12 archive variants remain byte-identical; the `NetPace.Core` NuGet package gains AOT-compatibility metadata. Smoke test (`--version`, `--help`, `servers`) runs on each AOT archive on its native runner before release attachment.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0
**Primary Dependencies**: `Spectre.Console`, `System.CommandLine`, `Microsoft.Extensions.DependencyInjection`, `ByteSize`, xUnit. `Humanizer` to be removed from `NetPace.Console`.
**Storage**: N/A (CLI tool; transient HTTP responses only)
**Testing**: xUnit (`NetPace.Core.Tests`, `NetPace.Console.Tests`); release-job smoke tests in workflow (`netpace --version | --help | servers`).
**Target Platform**: Cross-platform .NET 8.0 (Windows/Linux/macOS x64+arm64). New AOT publishes target `linux-x64` and `linux-arm64` only.
**Project Type**: CLI tool (`NetPace.Console`) plus reusable library (`NetPace.Core`, NuGet-published).
**Performance Goals**: AOT archive size MUST be smaller than the matching `-standalone` archive per RID (size-assertion gate, mirrors existing `framework-dependent < self-contained` check).
**Constraints**: Zero `IL2026`/`IL2090`/`IL3050`/`IL3056` warnings on `dotnet build`; AOT publish completes with those codes elevated to errors. Single-file flag MUST be omitted on AOT publishes. Invariant globalization enabled for AOT only. No `Directory.Build.props`; per-project `IsAotCompatible` only.
**Scale/Scope**: 14 release archives per tag (12 unchanged + 2 new). Two new GitHub-hosted runners (`ubuntu-latest`, `ubuntu-24.04-arm`). Three reflection-using sites to address: `XmlExtensions.DeserializeFromXml<T>`, two `Humanizer` call sites in `DefaultConsoleWriter.cs`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. TDD (NON-NEGOTIABLE) | PASS | XML parser rewrite gets RED unit tests in `NetPace.Core.Tests` first (server-list parsing fixtures, malformed XML, missing attributes). `Humanizer` replacement gets RED tests for `TimeSpan` formatting. Workflow changes are validated via release-job smoke test (functional gate, not unit-testable). |
| II. Library-First Architecture | PASS | All AOT-readiness work in `NetPace.Core` is internal/refactor — public surface unchanged except for `IsAotCompatible=true` metadata declaration. No new Console→Core coupling introduced. |
| III. CLI Excellence | PASS | No CLI behaviour change. Smoke test exercises `--version`, `--help`, `servers` end-to-end. |
| IV. Cross-Platform Compatibility | PASS | Existing 12 variants unchanged. New AOT variants are Linux-only by explicit scope; Windows/macOS AOT deferred. |
| V. Code Quality Standards | PASS | Zero-warnings rule extended: `IL2026/IL2090/IL3050/IL3056` treated as errors on AOT publish; warning analyzers active during `dotnet build` via `IsAotCompatible=true`. |
| VI. Minimal Dependencies | PASS — net negative | One dependency removed (`Humanizer`); zero added. `XmlExtensions` rewrite uses BCL `XDocument`/`XmlReader`. |
| VII. Semantic Versioning | PASS | `IsAotCompatible=true` on `NetPace.Core` is metadata-only; no source-breaking change to consumers. Falls under MINOR (new capability advertised). |
| VIII. AC-to-Test Traceability | PASS | Acceptance scenarios in `spec.md` use the Given/When/Then form. Scenario `**Scenario:**` labels and `test-plan.md` linkage are produced by `/speckit.testplan` (not this command). |

**Initial gate**: PASS — no violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-linux-aot-release/
├── plan.md              # This file
├── research.md          # Phase 0: AOT research, XML rewrite, Humanizer removal, runner choice
├── data-model.md        # Phase 1: release-archive entity, matrix entry, NuGet metadata
├── quickstart.md        # Phase 1: how to run an AOT publish locally and validate
├── contracts/
│   ├── release-archives.md       # External contract: archive name + content per release
│   └── nuget-package-metadata.md # External contract: NetPace.Core NuGet metadata signal
├── checklists/
│   └── requirements.md  # Spec-quality checklist (already created by /speckit.specify)
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
.github/workflows/
├── release-binaries.yml          # MODIFY: add 2 matrix.include entries, AOT smoke test, AOT size assertion
└── publish-nuget.yml             # UNCHANGED (consumes IsAotCompatible from csproj)

src/
├── NetPace.Core/
│   ├── NetPace.Core.csproj                                 # MODIFY: add IsAotCompatible=true
│   └── Clients/Ookla/
│       ├── OoklaServer.cs                                  # MODIFY: drop XmlAttribute/XmlRoot decoration (no longer used by serializer; keep types clean)
│       ├── OoklaServerList.cs                              # MODIFY: drop XmlRoot/XmlArray attributes
│       └── Extensions/
│           └── XmlExtensions.cs                            # REWRITE: XDocument/XmlReader-based parser, no XmlSerializer
├── NetPace.Console/
│   ├── NetPace.Console.csproj                              # MODIFY: add IsAotCompatible=true; remove Humanizer PackageReference
│   └── ConsoleWriters/
│       ├── DefaultConsoleWriter.cs                         # MODIFY: replace .Humanize() with hand-rolled TimeSpan formatter
│       └── MinimalConsoleWriter.cs                         # MODIFY: drop `using Humanizer;` (and any usage if present)
└── NetPace.Benchmarks/                                     # UNCHANGED (out of AOT publish path)

tests/
├── NetPace.Core.Tests/
│   └── Clients/Ookla/Extensions/
│       └── XmlExtensionsTests.cs                           # NEW or EXTEND: parser tests with Ookla XML fixtures
└── NetPace.Console.Tests/
    └── ConsoleWriters/
        └── (TimeSpanFormatterTests.cs)                     # NEW: cover hand-rolled humanize replacement

docs/
├── RELEASING.md                                            # NEW: release matrix, naming convention, runner-per-RID rationale
├── conventions/change-intent-records.md                    # REFERENCE: CIR template
└── (CIR for AOT — written alongside implementation PR)

README.md                                                   # MODIFY: install table — add AOT rows; IoT recommendation
USER_GUIDE.md                                               # MODIFY: variant-selection guidance
CHANGELOG.md                                                # MODIFY: next-release entry
```

**Structure Decision**: Follow the existing two-project layout (`NetPace.Core` library + `NetPace.Console` app) with mirroring tests. No new project. Workflow is the integration boundary; CIR captures the public-API metadata addition (`IsAotCompatible`) and the `XmlSerializer` → `XDocument` rewrite (internal to `NetPace.Core` but consequential — XML is an external wire format).

## Complexity Tracking

> No Constitution Check violations — table empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| _none_ | _n/a_ | _n/a_ |
