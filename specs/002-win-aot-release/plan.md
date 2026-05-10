# Implementation Plan: Windows Native AOT Release Artifacts

**Branch**: `002-win-aot-release` | **Date**: 2026-05-10 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-win-aot-release/spec.md`

## Summary

Extend the existing release-binaries matrix with two Windows Native AOT entries — `(win-x64, aot, windows-latest)` and `(win-arm64, aot, windows-11-arm)` — reusing the AOT publish flags, smoke-test contract, archive step, and size-assertion contract delivered by feature 001 (Linux AOT, GitHub issue #176). The work is workflow plumbing plus three documentation updates; no production C# changes.

## Technical Context

**Language/Version**: .NET 8.0 / C# 12 (unchanged)
**Primary Dependencies**: Spectre.Console 0.54.0, System.CommandLine 2.0.1, ByteSize 2.1.2, Microsoft.Extensions.DependencyInjection 9.0.9 (unchanged — already proven AOT-clean by feature 001)
**Storage**: N/A
**Testing**: xUnit (existing test projects); release-time smoke gate runs `NetPace.exe --version` / `NetPace.exe --help` on each Windows runner
**Target Platform**: GitHub Actions release pipeline (`.github/workflows/release-binaries.yml`); end-user targets are Windows x64 and Windows ARM64
**Project Type**: CLI tool (single project: `NetPace.Console` produces the binary, `NetPace.Core` ships separately on NuGet)
**Performance Goals**: AOT archive size strictly less than `-standalone` counterpart for the same RID (enforced by existing size-assertion job); end-user cold-start latency parity with the Linux AOT story (sub-50 ms — incidental, not a tracked KPI for this feature)
**Constraints**:
  - Native AOT cannot be cross-OS-compiled — `windows-latest` host required for `win-x64`, `windows-11-arm` host required for `win-arm64`.
  - AOT publishes must keep the existing `WarningsAsErrors=IL2026;IL2090;IL3050;IL3056` clean — no new trim/AOT warnings introduced.
  - No `.pdb` may leak into the archive (acceptance criterion in the spec).
  - Existing 14 archive contents must remain byte-identical for the same source state.
**Scale/Scope**: 2 new matrix entries · 1 workflow file edit · 3 documentation files updated · 0 source files changed.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. TDD (NON-NEGOTIABLE) | PASS — N/A for production code | This feature changes only workflow YAML and documentation. No new production C# code is added; no failing test is required ahead of YAML edits. The release-time smoke step (`--version` / `--help` exit `0`) is itself the test gating the new artefacts, mirroring feature 001. |
| II. Library-First Architecture | PASS | No `NetPace.Core` API changes. Public surface is unchanged; no new dependency added. |
| III. CLI Excellence | PASS | No CLI option changes. The two new artefacts expose the existing CLI behaviour; `--help` / `--version` continue to work via the same code path. |
| IV. Cross-Platform Compatibility | PASS — REINFORCED | This feature *extends* cross-platform support. The AOT/trim warning policy already declared on both projects (`IsAotCompatible=true`) catches platform-divergent reflection at compile time, before the matrix even reaches the new Windows runners. |
| V. Code Quality Standards | PASS | No production-code edits; the existing zero-warning posture is preserved by the warnings-as-errors block in `NetPace.Console.csproj`. |
| VI. Minimal Dependencies | PASS | Zero new dependencies. The change is purely workflow + docs. |
| VII. Semantic Versioning | PASS | No public-API change in `NetPace.Core`; therefore no version bump triggered by this feature itself. The new artefacts ship under whatever semver tag is next pushed. |
| VIII. AC-to-Test Traceability | PASS — DEFERRED | Acceptance scenarios in `spec.md` will gain `**Scenario:**` labels when `/speckit.testplan` runs and authors `test-plan.md`. The labels live alongside the scenarios; no constitution violation at plan time. |

**Gate result**: PASS. No violations to track in §Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/002-win-aot-release/
├── plan.md              # This file
├── research.md          # Phase 0 output — runner availability, .pdb handling, smoke shell
├── data-model.md        # Phase 1 output — release archive + matrix-entry schemas
├── quickstart.md        # Phase 1 output — how to dry-run the extended matrix
├── contracts/
│   └── release-matrix.md  # The 16-archive contract this feature must produce
├── checklists/
│   └── requirements.md  # Already created by /speckit.specify
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
.github/
└── workflows/
    └── release-binaries.yml          # ← edited: 2 new matrix.include entries; archive/smoke steps already handle Windows

src/                                  # ← UNCHANGED
├── NetPace.Console/
│   └── NetPace.Console.csproj        # AOT flags already in place (feature 001)
└── NetPace.Core/
    └── NetPace.Core.csproj           # IsAotCompatible=true already in place

docs/
├── RELEASING.md                      # ← edited: matrix table + runner-per-RID table
└── conventions/                      # unchanged

README.md                             # ← edited: install table grows by 2 rows
USER_GUIDE.md                         # ← edited: AOT-on-Windows availability note
```

**Structure Decision**: No new source modules. This feature is a **workflow + documentation patch** layered onto the existing single-project CLI structure. The four files actually edited at implementation time are `.github/workflows/release-binaries.yml`, `docs/RELEASING.md`, `README.md`, and `USER_GUIDE.md`. `CHANGELOG.md` is **not** touched (project has no CHANGELOG; release notes are GitHub-auto-generated — confirmed in `docs/RELEASING.md` §Release notes and project memory).

## Complexity Tracking

> No constitutional violations. Section intentionally empty.
