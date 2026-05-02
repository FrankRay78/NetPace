---
description: "Task list for feature 001-linux-aot-release"
---

# Tasks: Linux Native AOT Release Artifacts

**Input**: Design documents from `D:\Source\Repos\NetPace\specs\001-linux-aot-release\`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md), [test-plan.md](./test-plan.md)

**Tests**: Tests are REQUIRED — Constitution I (TDD) is non-negotiable. Every test method MUST include a `// SCENARIO: <header text>` comment whose value matches a `#### Scenario:` heading in [test-plan.md](./test-plan.md) exactly.

**Organization**: Tasks are grouped by user story. User Story 1 is the MVP; the foundational phase is unavoidable (NetPace.Core AOT-readiness underpins both US1 and US3).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: User-story label (US1, US2, US3, US4) — omitted on Setup, Foundational, and Polish tasks

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify environment is ready for AOT-related work.

- [ ] T001 Verify `.NET 8 SDK` (`dotnet --version` reports `8.0.x`) and clean state on branch `001-linux-aot-release` in repo root `D:\Source\Repos\NetPace\`
- [ ] T002 Run `dotnet restore src/NetPace.sln` and `dotnet build src/NetPace.sln -c Release` from a clean state to confirm baseline build is green before any change

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: NetPace.Core AOT-readiness — the XML parser rewrite + `IsAotCompatible` declaration. These changes underpin **both** US1 (Linux AOT publish) and US3 (NuGet AOT metadata). No user-story phase can begin until this completes.

**⚠️ CRITICAL**: No US1/US3 work can start before T009 passes.

### Tests for Foundational (RED first per Constitution I)

- [ ] T003 [P] Capture a representative Ookla `/speedtest-config.php` response and add it as an embedded resource at `src/NetPace.Core.Tests/Clients/Ookla/Resources/ookla-servers-sample.xml` (≥5 `<server>` entries; some with optional `country`/`host`, some without)
- [ ] T004 [P] Write 6 RED tests in `src/NetPace.Core.Tests/Clients/Ookla/Extensions/XmlExtensionsTests.cs` covering FR-010 scenarios from [test-plan.md](./test-plan.md) — each test method includes the `// SCENARIO:` comment for its scenario name:
  - `// SCENARIO: Parser deserializes a representative Ookla server-list response`
  - `// SCENARIO: Parser populates optional attributes when present`
  - `// SCENARIO: Parser leaves optional attributes null when absent`
  - `// SCENARIO: Parser uses invariant culture for numeric attribute parsing`
  - `// SCENARIO: Parser handles an empty servers element`
  - `// SCENARIO: Parser throws on malformed XML`
- [ ] T005 Run `dotnet test src/NetPace.Core.Tests` and confirm the 6 new tests FAIL (RED) before any implementation

### Implementation for Foundational

- [ ] T006 Rewrite `src/NetPace.Core/Clients/Ookla/Extensions/XmlExtensions.cs` — replace the `XmlSerializer`-based body of `DeserializeFromXml<T>` with an `XDocument`/`XmlReader`-based parser specialised for `OoklaServerList` (use `XDocument.Parse` → root `<settings>` → `<servers>` → `<server>` elements; read attributes directly; numeric attributes via `double.Parse(..., CultureInfo.InvariantCulture)` and `int.Parse(..., CultureInfo.InvariantCulture)`; missing required attributes throw `XmlException`). Keep the public method signature (`internal static T? DeserializeFromXml<T>(this string data)`) — body specialises only when `T == typeof(OoklaServerList)`, else throw `NotSupportedException` (no other type is a caller today).
- [ ] T007 Remove `[XmlAttribute("...")]` decoration from every property of `src/NetPace.Core/Clients/Ookla/OoklaServer.cs`
- [ ] T008 Remove `[XmlRoot("settings")]`, `[XmlArray("servers")]`, `[XmlArrayItem("server")]` decoration from `src/NetPace.Core/Clients/Ookla/OoklaServerList.cs`
- [ ] T009 Run `dotnet test src/NetPace.Core.Tests` and confirm all 6 new tests pass (GREEN); existing tests still pass
- [ ] T010 Add `<IsAotCompatible>true</IsAotCompatible>` inside the existing `<PropertyGroup>` of `src/NetPace.Core/NetPace.Core.csproj`
- [ ] T011 Run `dotnet build src/NetPace.Core/NetPace.Core.csproj -c Release -warnaserror:IL2026,IL2090,IL3050,IL3056` and confirm zero IL warnings, exit 0

**Checkpoint**: NetPace.Core is AOT-clean and the XML parser is reflection-free. US1 and US3 are now unblocked.

---

## Phase 3: User Story 1 — Download a Native AOT binary for a Linux IoT device (Priority: P1) 🎯 MVP

**Goal**: Tag-driven release pipeline produces `netpace-{tag}-linux-x64-aot.tar.gz` and `netpace-{tag}-linux-arm64-aot.tar.gz` archives that contain a single native ELF binary, pass smoke tests on their native runners, and are smaller than their `-standalone` counterparts.

**Independent Test**: Tag a release, observe the two new archives appear; download and run `./netpace --version`, `./netpace --help`, `./netpace servers` on a matching Linux host with no .NET runtime installed; all three exit `0`.

### Tests for User Story 1 (RED first)

- [ ] T012 [P] [US1] Write 4 RED tests in `src/NetPace.Console.Tests/ConsoleWriters/TimeSpanFormatterTests.cs` covering FR-011 scenarios — each test method includes the `// SCENARIO:` comment for its scenario name:
  - `// SCENARIO: Replacement formatter produces "1 second" for one-second TimeSpan`
  - `// SCENARIO: Replacement formatter pluralises for multi-second TimeSpan`
  - `// SCENARIO: Replacement formatter rounds fractional seconds to whole seconds`
  - `// SCENARIO: Replacement formatter handles zero TimeSpan defensively`
- [ ] T013 [US1] Run `dotnet test src/NetPace.Console.Tests` and confirm the 4 new tests FAIL (RED)

### Implementation for User Story 1 — Console AOT-readiness

- [ ] T014 [US1] Add new file `src/NetPace.Console/ConsoleWriters/TimeSpanFormatter.cs` implementing `internal static class TimeSpanFormatter` with `internal static string Humanize(this TimeSpan ts)` matching Humanizer's `precision: 1` output for 0–600 seconds (singular/plural, fractional rounded to nearest whole second, zero/negative → `"0 seconds"`)
- [ ] T015 [US1] Run `dotnet test src/NetPace.Console.Tests` and confirm the 4 tests pass (GREEN)
- [ ] T016 [US1] Replace `using Humanizer;` with `using NetPace.Console.ConsoleWriters;` in `src/NetPace.Console/ConsoleWriters/DefaultConsoleWriter.cs` and confirm the existing `elapsed.Humanize()` call sites (lines ~100 and ~106) now bind to the new internal extension
- [ ] T017 [US1] Remove `using Humanizer;` from `src/NetPace.Console/ConsoleWriters/MinimalConsoleWriter.cs` (the import is dead today; keep file functionally unchanged)
- [ ] T018 [US1] Remove `<PackageReference Include="Humanizer" Version="2.14.1" />` from `src/NetPace.Console/NetPace.Console.csproj`
- [ ] T019 [US1] Add `<IsAotCompatible>true</IsAotCompatible>` inside the existing `<PropertyGroup>` of `src/NetPace.Console/NetPace.Console.csproj`
- [ ] T020 [US1] Run `dotnet build src/NetPace.sln -c Release -warnaserror:IL2026,IL2090,IL3050,IL3056` from clean and confirm zero IL warnings, exit 0 (covers FR-008 scenario "Solution build emits zero AOT/trim warnings")

### Implementation for User Story 1 — Local AOT validation

- [ ] T021 [US1] Run the linux-x64 AOT publish per [quickstart.md](./quickstart.md) step 2 and confirm exit 0; verify output directory contains exactly one ELF file named `netpace`, no `*.dll`, no `*.deps.json`, no `runtimes/` directory (covers FR-009 + FR-003 locally before CI is wired)
- [ ] T022 [US1] Run [quickstart.md](./quickstart.md) step 4 (`./netpace --version`, `./netpace --help`, `./netpace servers`) on the locally-built AOT binary and confirm all three exit 0 — `servers` exercises the new `XmlExtensions` parser end-to-end under AOT

### Implementation for User Story 1 — Release pipeline

- [ ] T023 [US1] Edit `.github/workflows/release-binaries.yml`:
  - Change job `runs-on: ubuntu-latest` to `runs-on: ${{ matrix.runs_on || 'ubuntu-latest' }}` so AOT entries can override
  - Append two `matrix.include:` entries (after the existing `runtime` and `deployment` axes):
    - `{ runtime: linux-x64,   deployment: aot, runs_on: ubuntu-latest,    publish_aot: true, publish_single_file: false, invariant_globalization: true }`
    - `{ runtime: linux-arm64, deployment: aot, runs_on: ubuntu-24.04-arm, publish_aot: true, publish_single_file: false, invariant_globalization: true }`
- [ ] T024 [US1] Edit the `Set deployment flags` and `Publish Console App` steps in `.github/workflows/release-binaries.yml` so:
  - `suffix=-aot` when `matrix.deployment == 'aot'`
  - `self_contained=true` when `matrix.deployment == 'aot'`
  - The `dotnet publish` invocation conditionally appends `-p:PublishAot=true -p:InvariantGlobalization=true -p:WarningsAsErrors=IL2026,IL2090,IL3050,IL3056` and **omits** `-p:PublishSingleFile=true` when `matrix.deployment == 'aot'` (covers FR-013, FR-015)
- [ ] T025 [US1] Add a new `Smoke test (AOT only)` step to the matrix job in `.github/workflows/release-binaries.yml`, conditional on `matrix.deployment == 'aot'`, that extracts the produced `.tar.gz` to a temp directory and runs `./netpace --version`, `./netpace --help`, `./netpace servers` sequentially with `set -e` so any non-zero exit fails the job (covers FR-005 scenarios)
- [ ] T026 [US1] Extend the existing `Verify framework-dependent binaries are smaller than standalone` step in the `attach-to-release` job of `.github/workflows/release-binaries.yml` to additionally assert, for `runtime ∈ { linux-x64, linux-arm64 }`, that the `-aot.tar.gz` artifact is strictly smaller than the matching `-standalone.tar.gz`; fail the job otherwise (covers FR-004 scenarios)
- [ ] T027 [US1] Run `actionlint` (or visually verify) the modified `release-binaries.yml` parses cleanly; ensure existing 12 matrix entries (runtime grid × deployment grid) are unchanged in source bytes (covers FR-014 scenario "Pre-existing matrix grid is unchanged")
- [ ] T028 [US1] Push a pre-release tag (e.g. `0.6.0-rc.1`) and observe: 14 archives published, AOT smoke tests pass on both runners, AOT size assertion passes for both Linux RIDs (covers FR-001, FR-005, FR-004 end-to-end)

**Checkpoint**: User Story 1 is fully functional — the IoT/embedded user can download and run a Linux AOT binary.

---

## Phase 4: User Story 2 — Existing variants unchanged (Priority: P1)

**Goal**: Pre-existing 12 archives remain present, named identically, and functionally equivalent. NuGet workflow runs unchanged.

**Independent Test**: Diff the asset filename list and the workflow YAML against the pre-feature tag; verify no rename, no removal, no behavioural drift in the 12 entries; verify `publish-nuget.yml` is byte-identical.

### Verification for User Story 2

- [ ] T029 [P] [US2] Compare `git diff main..HEAD -- .github/workflows/release-binaries.yml` and confirm the 12 pre-existing matrix combinations are byte-identical; only additions are the two new `matrix.include` entries, the new smoke-test step, and the extended size-assertion logic (covers FR-002 + FR-014 "Pre-existing matrix grid is unchanged")
- [ ] T030 [P] [US2] Confirm `git diff main..HEAD -- .github/workflows/publish-nuget.yml` is empty (covers FR-018 scenario "publish-nuget.yml contents unchanged")
- [ ] T031 [US2] On the pre-release tag run from T028, list the GitHub Release assets and assert exactly 14 files: the 12 pre-existing names plus the 2 new AOT archives; no other names present (covers FR-002 scenario "All 12 pre-existing archive filenames present after change")

**Checkpoint**: Backwards compatibility verified.

---

## Phase 5: User Story 3 — NuGet consumers see AOT-compatibility metadata (Priority: P2)

**Goal**: The published `NetPace.Core` NuGet package advertises `IsAotCompatible=true` so AOT-publishing consumers see no AOT warnings originating from `NetPace.Core`.

**Independent Test**: `dotnet pack` `NetPace.Core` and inspect the `.nupkg`; in a separate AOT-published consumer project reference the package and confirm zero AOT warnings reference `NetPace.Core` types.

### Implementation for User Story 3

- [ ] T032 [US3] Run `dotnet pack src/NetPace.Core/NetPace.Core.csproj -c Release -o ./artifacts/nupkg` and extract the resulting `NetPace.Core.{ver}.nupkg`; inspect the embedded `NetPace.Core.nuspec` and confirm AOT-compatibility metadata is present (either as an explicit element or via the marker NuGet emits when `IsAotCompatible=true`) — covers FR-006 scenario "Published NetPace.Core nupkg declares AOT compatibility"
- [ ] T033 [US3] Create a throwaway AOT consumer project (`dotnet new console -n AotConsumer`), reference the locally-packed `NetPace.Core.{ver}.nupkg` from a local feed, call `OoklaSpeedtest` from `Main`, run `dotnet publish -c Release -r linux-x64 -p:PublishAot=true -p:InvariantGlobalization=true -warnaserror:IL2026,IL2090,IL3050,IL3056`, and confirm exit 0 with zero warnings referencing any `NetPace.Core.*` symbol — covers FR-006 scenario "AOT consumer of NetPace.Core sees no AOT warnings from the package"

**Checkpoint**: NuGet consumers can build AOT applications cleanly against `NetPace.Core`.

---

## Phase 6: User Story 4 — Releasing documentation (Priority: P3)

**Goal**: A new contributor can identify variants, runners, naming convention, and the recommended IoT download from documentation alone.

**Independent Test**: A contributor unfamiliar with the project reads `docs/RELEASING.md` and the README install table; they can list the variants, naming pattern, and rationale for each without opening workflow YAML.

### Implementation for User Story 4

- [ ] T034 [P] [US4] Edit `D:\Source\Repos\NetPace\README.md` install table to add two new rows for `netpace-{ver}-linux-x64-aot.tar.gz` and `netpace-{ver}-linux-arm64-aot.tar.gz`; add a one-line note that AOT is the recommended download for IoT/embedded Linux deployments
- [ ] T035 [P] [US4] Edit `D:\Source\Repos\NetPace\USER_GUIDE.md` to add a "Choosing a download" section with three bullets comparing AOT, self-contained, and framework-dependent (per [research.md](./research.md) R-8)
- [ ] T036 [P] [US4] Edit `D:\Source\Repos\NetPace\CHANGELOG.md` to add an entry for the next release: "Added: Linux Native AOT release artifacts (`linux-x64-aot`, `linux-arm64-aot`). Removed: `Humanizer` dependency from `NetPace.Console`. Internal: `OoklaServerList` XML parsing rewritten to be AOT-safe (no public-API change)."
- [ ] T037 [P] [US4] Create new file `D:\Source\Repos\NetPace\docs\RELEASING.md` documenting the 14-archive release matrix table, runner-per-RID rationale (`ubuntu-latest` for x64, `ubuntu-24.04-arm` for arm64), naming convention `{name}-{ver}-{rid}-{variant}.{ext}`, smoke-test contract (`--version`/`--help`/`servers`), per-RID size-assertion contract, and placeholder sections for future Windows/macOS AOT follow-ups (per [research.md](./research.md) R-8)

**Checkpoint**: All four user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: CIR, regression confirmation, and the quickstart end-to-end gate.

- [ ] T038 Author a single Change Intent Record per `D:\Source\Repos\NetPace\docs\conventions\change-intent-records.md` covering: (a) `IsAotCompatible=true` public-API metadata addition on `NetPace.Core`, (b) `XmlSerializer` → `XDocument`/`XmlReader` rewrite plus removal of `[XmlAttribute]`/`[XmlRoot]`/`[XmlArray]`/`[XmlArrayItem]` decorations, (c) release-pipeline extension to 14 archives. Place it where the convention specifies (filename TBD by the convention doc) and reference it from the implementation PR description (covers FR-017)
- [ ] T039 Run full regression: `dotnet test src/NetPace.sln -c Release` from clean state; confirm exit 0 with zero failed tests across `NetPace.Core.Tests` and `NetPace.Console.Tests` (covers FR-019)
- [ ] T040 Walk through [quickstart.md](./quickstart.md) end-to-end on a clean Linux x64 host as a final acceptance gate; confirm every step produces the expected outcome
- [ ] T041 Run `/speckit.testchecklist` (or the equivalent traceability check) over `src/NetPace.Core.Tests/Clients/Ookla/Extensions/XmlExtensionsTests.cs` and `src/NetPace.Console.Tests/ConsoleWriters/TimeSpanFormatterTests.cs` to confirm every `#### Scenario:` from [test-plan.md](./test-plan.md) FR-010 and FR-011 is referenced by a `// SCENARIO:` comment
- [ ] T042 Update [spec.md](./spec.md) acceptance scenarios to add `**Scenario:**` labels matching the `#### Scenario:` headers in [test-plan.md](./test-plan.md), satisfying Constitution VIII before `/speckit.analyze`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies
- **Phase 2 (Foundational)**: depends on Phase 1; **blocks** US1 and US3
- **Phase 3 (US1)**: depends on Phase 2 completion (specifically T011)
- **Phase 4 (US2)**: depends on Phase 3 completion (T028 produces the artefacts US2 verifies)
- **Phase 5 (US3)**: depends on Phase 2 completion only (independent of US1)
- **Phase 6 (US4)**: depends on Phase 3 completion (US1 produces the artefacts the docs describe; nothing in US2/US3 changes the user-facing download surface)
- **Phase 7 (Polish)**: depends on all desired user stories complete

### Within-Phase Dependencies (Phase 2)

- T003 ∥ T004 (different files; tests file imports the resource file)
- T005 (run RED) depends on T003 + T004
- T006 (parser rewrite) depends on T005 (must see RED first)
- T007 ∥ T008 (different csproj-adjacent files; both are decoration removals — independent)
- T009 (run GREEN) depends on T006 + T007 + T008
- T010 (`IsAotCompatible` on Core) can run after T009
- T011 (build verification) depends on T010

### Within-Phase Dependencies (Phase 3 — US1)

- T012 → T013 (RED) → T014 → T015 (GREEN) — TDD chain for the formatter
- T016 ∥ T017 (different files) but both require T014 (the new formatter must exist for `using` to compile)
- T018 (remove Humanizer dep) depends on T016 + T017 (no remaining `using Humanizer;`)
- T019 (`IsAotCompatible` on Console) ∥ T018 (different concerns)
- T020 (full-solution build verification) depends on T015 + T016 + T017 + T018 + T019
- T021 (local AOT publish) depends on T020
- T022 (local smoke test) depends on T021
- T023, T024, T025, T026 are sequential edits to the same workflow file (`release-binaries.yml`) — no [P]
- T027 (workflow lint) depends on T023–T026
- T028 (pre-release tag) depends on T022 + T027

### Within-Phase Dependencies (Phase 4 — US2)

- T029 ∥ T030 (different files)
- T031 depends on T028 having produced a pre-release set

### Within-Phase Dependencies (Phase 5 — US3)

- T032 → T033 (consumer project needs the locally-packed nupkg from T032)

### Within-Phase Dependencies (Phase 6 — US4)

- T034 ∥ T035 ∥ T036 ∥ T037 (all different files)

### Parallel Opportunities Across Stories

- US3 (Phase 5) and US1 (Phase 3) can in principle proceed in parallel after Phase 2 completes; the only shared artefact is the locally-packed nupkg in T032 which is independent of US1 work.
- US4 (Phase 6) docs writing can begin in parallel with US1 release-pipeline work (T023+) once the local AOT validation (T022) confirms the binary actually works.

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Launch RED test groundwork in parallel:
Task: "Capture sample Ookla XML at src/NetPace.Core.Tests/Clients/Ookla/Resources/ookla-servers-sample.xml"   # T003
Task: "Write 6 RED XmlExtensions tests at src/NetPace.Core.Tests/Clients/Ookla/Extensions/XmlExtensionsTests.cs" # T004

# After RED is confirmed, the parser rewrite is sequential (single file).
# Then attribute removals can run in parallel:
Task: "Remove XmlAttribute decoration from OoklaServer.cs"     # T007
Task: "Remove XmlRoot/XmlArray/XmlArrayItem from OoklaServerList.cs"  # T008
```

## Parallel Example: Phase 6 (Documentation)

```bash
Task: "Update README.md install table"               # T034
Task: "Add Choosing a Download section to USER_GUIDE.md"  # T035
Task: "Add CHANGELOG.md entry for next release"      # T036
Task: "Create docs/RELEASING.md"                     # T037
```

---

## Implementation Strategy

### MVP Scope

**MVP = Phase 1 + Phase 2 + Phase 3 (User Story 1)**

This delivers the headline value: a Linux IoT/embedded user can download and run a native AOT binary. Phases 4–6 are verification + documentation + NuGet metadata polish; Phase 7 is housekeeping.

### Incremental Delivery

1. Phase 1 → Phase 2 → confirm `dotnet build` is AOT-clean for `NetPace.Core` → MVP foundation ready.
2. Phase 3 → tag `0.6.0-rc.1` → verify 14 archives, smoke tests, size assertion → **MVP shipped**.
3. Phase 4 (US2 verification) → confirms backwards compat — gates the `-rc` → final-tag promotion.
4. Phase 5 (US3 NuGet validation) — independent of MVP; can ship in same release.
5. Phase 6 (US4 docs) → ship in same release.
6. Phase 7 → CIR + final regression → tag final `0.6.0`.

### Solo-Developer Order (recommended)

1. T001 → T002 (Setup)
2. T003 → T004 → T005 → T006 → T007 → T008 → T009 → T010 → T011 (Foundational, strict order — TDD)
3. T012 → T013 → T014 → T015 → T016 → T017 → T018 → T019 → T020 → T021 → T022 (US1 code work)
4. T023 → T024 → T025 → T026 → T027 (US1 workflow edits — same file)
5. T028 (pre-release tag — gates rest)
6. T029 ∥ T030 → T031 (US2 verification, parallel pair then check)
7. T032 → T033 (US3)
8. T034 ∥ T035 ∥ T036 ∥ T037 (US4 docs in parallel)
9. T038 → T039 → T040 → T041 → T042 (Polish)

### Parallel Team Strategy

With 2 developers after Phase 2:

- Dev A: US1 (Phase 3 — high concentration on workflow YAML and AOT validation)
- Dev B: US3 (Phase 5) → US4 (Phase 6) → flow into Polish

US2 verification (Phase 4) is a single sit-down review by either developer once T028 lands.

---

## Notes

- Every task in Phase 2 and Phase 3 implementing test scenarios from [test-plan.md](./test-plan.md) **must** add the `// SCENARIO: <header text>` traceability comment per the implementation guidance in test-plan.md.
- T028 (pre-release tag push) is the natural integration gate — do not promote to a final tag until T028 + Phases 4/5 verify cleanly.
- Avoid scope creep: Windows/macOS AOT, code signing, `Directory.Build.props`, and self-contained deprecation are explicitly out of scope per [spec.md](./spec.md). If those temptations surface during implementation, capture as follow-up issues, not as additions to this task list.
- Commit cadence: commit after each completed task or each tight TDD cycle (RED+GREEN+REFACTOR). Do not commit on red.
