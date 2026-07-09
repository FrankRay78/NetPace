# Tasks: Add `--profile` CLI switch (Tiny/Small/Medium/Large/Mega)

**Input**: Design documents from `/specs/003-profile-cli-switch/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md, test-plan.md

**Tests**: REQUIRED by constitution principle I (TDD is non-negotiable). Every public-API surface introduced here must be added test-first (RED → GREEN → REFACTOR). Every test that implements a `**Scenario:**` from spec.md MUST carry a matching `// SCENARIO:` comment (see `test-plan.md` for the canonical names).

**Organization**: Tasks are grouped by user story. The five profile arms live in one inline switch in one file, so the *implementation* code is largely shared (Phase 2 Foundational), while the *acceptance tests* and *adjacent docs* are sliced per user story (Phases 3–7). Within each user-story phase, write the test first, watch it fail, then implement the corresponding switch arm and any per-story docs — the constitution applies whether or not the implementation file is shared.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- File paths are absolute or anchored at the repo root.

---

## Phase 1: Setup

**Purpose**: Confirm the working environment is sane before changing public-API surface.

- [X] T001 Confirm branch `003-profile-cli-switch` is checked out and the baseline `dotnet build` and `dotnet test` both succeed against `main`'s current behaviour (no warnings, all tests green) — establishes the green baseline that subsequent RED tests must move from.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Type / file / wiring scaffold that every user story depends on — the `Profile` enum, the relocated cap properties, the deleted method overloads, the two new public constructors, and the `--profile` CLI binding. Tests in this phase cover invariants that span all user stories (no story label); per-story acceptance tests live in Phases 3–7.

**⚠️ CRITICAL**: No user-story work begins until Phase 2 is complete.

- [X] T002 [P] Add the public `Profile` enum at [src/NetPace.Core/Profile.cs](src/NetPace.Core/Profile.cs) with five members `Tiny`, `Small`, `Medium`, `Large`, `Mega` and XML documentation on every member. The `Profile.Mega` XML doc carries a one-line undocumented-payload caveat in this phase; the fuller cross-referenced warning is finalised in T017 (US3).
- [X] T003 [P] Add `DownloadSizeMb { get; init; } = int.MaxValue;` to [src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs](src/NetPace.Core/Clients/Ookla/Settings/DownloadTestSettings.cs) with XML `<summary>` and `<remarks>` disambiguating it from `DownloadSizes` (per data-model.md).
- [X] T004 [P] Add `UploadSizeMb { get; init; } = int.MaxValue;` to [src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs](src/NetPace.Core/Clients/Ookla/Settings/UploadTestSettings.cs) with XML `<summary>` and `<remarks>` (per data-model.md).
- [X] T005 Delete the four `int sizeMb` overloads on [src/NetPace.Core/ISpeedTestService.cs](src/NetPace.Core/ISpeedTestService.cs) listed in `contracts/speedtestservice-surface.md` — leaves the `(server, ct)` and `(server, IProgress, ct)` overloads per direction; expect compile errors in `OoklaSpeedtest` and `Program.cs` until T006 and T013 land.
- [X] T006 Delete the matching overloads on [src/NetPace.Core/Clients/Ookla/OoklaSpeedtest.cs](src/NetPace.Core/Clients/Ookla/OoklaSpeedtest.cs); rewire the internal `maxBytes`-cap branch in `GenericTestSpeedAsync` (or equivalent loop) to read `settings.DownloadTest.DownloadSizeMb` / `settings.UploadTest.UploadSizeMb` from the settings record set at construction; update XML docs on surviving methods to remove references to the deleted `int sizeMb` parameter.
- [X] T007 [P] Write structural test file [src/NetPace.Core.Tests/ProfileTests.cs](src/NetPace.Core.Tests/ProfileTests.cs) with: (a) namespace assertion — `typeof(NetPace.Core.Profile).Namespace == "NetPace.Core"`; (b) reflection assertion — no static method on any type in the `NetPace.Core` assembly takes `Profile` as its first parameter and returns a type whose namespace starts with `NetPace.Core.Clients`; (c) reflection assertion — no type named `OoklaSpeedtestSettingsExtensions` or `OoklaProfileExtensions` exists in the assembly; (d) file-existence assertion — `src/NetPace.Core/Profile.cs` exists at exactly that path. Carries the matching `// SCENARIO:` comments where these assertions cover US5 scenarios — see Phase 7.
- [X] T008 [P] Write [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs) covering the cross-story invariants: (a) `new OoklaSpeedtestSettings()` produces a record equal to `new OoklaSpeedtestSettings(Profile.Medium)`; (b) `new OoklaSpeedtestSettings((Profile)999)` throws `ArgumentOutOfRangeException` with `ParamName == "profile"`; (c) `new OoklaSpeedtestSettings(Profile.Mega) with { UseProxy = true }` preserves Mega's `DownloadTest`/`UploadTest` field values and sets `UseProxy == true`; (d) `OoklaSpeedtestSettings` instance state has no `Profile` property (reflection). Each method carries the matching `// SCENARIO:` comment for its US2/US5 scenario name — see Phases 4 and 7.
- [X] T009 [P] Write [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs) as a partial-class extension of T008's file, containing one `[Fact]` per profile that asserts every field of `DownloadTest` and `UploadTest` exactly matches the data-model.md tables. The Tiny/Small/Mega tests carry `// SCENARIO:` comments matching the test-plan names; Medium and Large get plain field-equality asserts without a scenario tag (they cover requirements FR-002..FR-008, not labelled spec scenarios). Each `[Fact]` is independently runnable.
- [X] T010 Implement both new public constructors on [src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs](src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs) per the declaration in `contracts/ooklasettings-ctors.md`: parameterless ctor chains via `: this(Profile.Medium) { }`; `OoklaSpeedtestSettings(Profile profile)` contains the entire profile → settings mapping as one inline switch expression with all five arms populated from data-model.md, plus a `_ => throw new ArgumentOutOfRangeException(nameof(profile))` default. Remove the existing `= new();` initializers from the `DownloadTest` and `UploadTest` property declarations (now set by the constructor). Verify T007–T009 turn GREEN.
- [X] T011 Add `public Profile Profile { get; init; } = Profile.Medium;` to [src/NetPace.Console/Commands/SpeedTestCommandSettings.cs](src/NetPace.Console/Commands/SpeedTestCommandSettings.cs) alongside the existing option-binding properties (mirroring `UnitSystem`, `UnitScale`, etc.).
- [X] T012 [P] Write [src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs](src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs) using the existing `CommandLineTestHost` pattern, covering the cross-story CLI invariants: case-insensitive enum parsing (`--profile TINY`, `--profile Tiny`, `--profile tiny`); unknown value rejection (`--profile huge` exits non-zero); option binding produces `SpeedTestCommandSettings.Profile == Profile.Tiny` for `--profile tiny`. (Per-scenario CLI tests covering specific user stories live in Phases 3–7 and extend this file.)
- [X] T013 Add `var profileOption = new Option<Profile>("--profile") { Description = "Profile bundle of payload settings (Tiny | Small | Medium | Large | Mega).", DefaultValueFactory = _ => Profile.Medium };` to [src/NetPace.Console/Program.cs](src/NetPace.Console/Program.cs) next to the existing `--unit-system` declaration; register it on the root command; bind onto `SpeedTestCommandSettings.Profile`. Rewire the `OoklaSpeedtestSettings` construction (currently at the per-issue-body L232–233 site) from "parameterless ctor + with-override of `DownloadTest`/`UploadTest`" to `new OoklaSpeedtestSettings(commandSettings.Profile)`; apply conditional `with { DownloadTest = settings.DownloadTest with { DownloadSizeMb = N } }` only when `--downloadsize` is explicitly supplied; same for `--uploadsize`; remove the now-deleted `int sizeMb` arguments from `GetDownloadSpeedAsync` / `GetUploadSpeedAsync` call sites. Verify T012 turns GREEN.
- [X] T014 [P] Refresh the `--help` Verify snapshot(s) under [src/NetPace.Console.Tests/Expectations/](src/NetPace.Console.Tests/Expectations/) so the `--profile <Tiny|Small|Medium|Large|Mega>` line (with `[default: Medium]`) is included. Run the affected `NetPaceConsoleTests` snapshot tests, accept the diff into the `.verified.txt` file(s).

**Checkpoint**: Public API surface, CLI binding, and cross-story invariant tests are all in place. `dotnet build` is warning-free; `dotnet test` is green. Profile values are field-for-field correct for all five profiles.

---

## Phase 3: User Story 1 — Run NetPace on a constrained data plan without busting the cap (Priority: P1) 🎯 MVP

**Goal**: Users on metered / IoT plans (Tiny ≤ 1 MiB total per run; Small ≤ 12 MiB total per run) can run NetPace within their data budget. The profile is authoritative for per-request payload size, so no individual download request fetches a full 4000-pixel JPEG when Tiny is selected.

**Independent Test**: Construct `new OoklaSpeedtestSettings(Profile.Tiny)`, assert all eight `DownloadTest`/`UploadTest` fields match the Tiny budget table from data-model.md; same for Small. Verify a CLI binding via `--profile tiny` produces no `DownloadSizes` entry > 350.

- [X] T015 [P] [US1] Add a `[Fact]` method to [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs) carrying `// SCENARIO: Tiny profile stays within IoT budget`. Assert `DownloadSizes == [350]`, `DownloadSizeIterations == 1`, `DownloadParallelTasks == 1`, `DownloadSizeMb == 1`, and the equivalent four Upload fields per data-model.md. Add an in-method comment recording the natural-transfer budget proxy (≤ 1 MiB total = 245 KB down + 50 KB up ±10 %) for future readers; do not assert this at runtime (per D8 — no Docker integration test).
- [X] T016 [P] [US1] Add a `[Fact]` method to the same partial file carrying `// SCENARIO: Small profile suits cellular`. Assert Small's eight field values per data-model.md; record the ≤ 12 MiB total proxy in a comment.
- [X] T017 [US1] Add a `[Fact]` method to [src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs](src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs) carrying `// SCENARIO: Profile is authoritative for per-request shape`. Invoke the option-binding code path with `--profile tiny`, intercept the constructed `OoklaSpeedtestSettings` (the existing `CommandLineTestHost` pattern provides a hook or test seam — extend if not present), assert `settings.DownloadTest.DownloadSizes` is exactly `[350]`, `DownloadParallelTasks == 1`, `DownloadSizeIterations == 1`, and assert by `.All(s => s <= 350)` that no entry exceeds 350.

**Checkpoint**: Tiny and Small profiles are usable end-to-end via the CLI; per-request shape authority is verified. US1 deliverable complete.

---

## Phase 4: User Story 2 — Get a sensible default without thinking about it (Priority: P1)

**Goal**: `netpace` with no flags runs Medium (~121 MiB total), a ≥ 65 % traffic reduction from the prior ~370 MiB default. The parameterless `OoklaSpeedtestSettings()` ctor chains to `Profile.Medium` as the single source of truth.

**Independent Test**: `netpace` with no `--profile` binding constructs `new OoklaSpeedtestSettings(Profile.Medium)` field-for-field; `new OoklaSpeedtestSettings()` parameterless matches the same; Medium's settings imply ≤ 130 MiB total transfer.

- [X] T018 [P] [US2] Add a `[Fact]` method to [src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs](src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs) carrying `// SCENARIO: Omitted --profile defaults to Medium`. Invoke the CLI with no `--profile` flag; assert the constructed `OoklaSpeedtestSettings` is equal (record equality) to `new OoklaSpeedtestSettings(Profile.Medium)`; assert `DownloadSizes == [1500, 2000, 3000, 3500, 4000]`, `DownloadSizeMb == 100`, `UploadSizeMb == 25`.
- [X] T019 [P] [US2] Locate the parameterless-ctor test method already written in T008 (Phase 2) at [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs); confirm or add the `// SCENARIO: Parameterless ctor chains to Medium` comment so `/speckit.testchecklist` can find it; assert all 8 fields under `DownloadTest` and `UploadTest` are field-for-field identical to Medium-profile values (extend the T008 assertion if it only asserts record equality).
- [X] T020 [P] [US2] Add a `[Fact]` method to [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs) carrying `// SCENARIO: Default-run traffic drops vs pre-change baseline`. Compute Medium's natural transfer-budget proxy from its settings (e.g. `(DownloadSizes.Length * DownloadSizeIterations * DownloadParallelTasks * AverageRequestBytes)` bounded by `DownloadSizeMb * 1024 * 1024` — use the per-request bytes derived from `docs/architecture/download-upload-size-controls.md` and committed to as test constants). Assert: the implied total is ≤ 130 MiB (i.e. ≤ 65 % of the 370 MiB prior baseline). Record in a comment that the "no functional regression in reported speeds" portion is covered by existing `OoklaSpeedtest` tests passing under the new defaults.

**Checkpoint**: Default-run traffic reduction verified; parameterless ctor's single-source-of-truth chain to Medium is locked in.

---

## Phase 5: User Story 3 — Saturate a 10 Gbps inter-DC link (Priority: P2)

**Goal**: `--profile mega` issues requests for the bonus payloads (5000/6000/7000) and pushes ~10 GiB total per run, enabling saturation of inter-DC fibre. The Mega-specific risk is documented in XML and cross-referenced to the architecture doc.

**Independent Test**: `new OoklaSpeedtestSettings(Profile.Mega).DownloadTest.DownloadSizes` includes 5000, 6000, and 7000; the assembly's XML doc file contains the undocumented-payload caveat for `Profile.Mega`.

- [X] T021 [US3] Expand the XML documentation on the `Profile.Mega` enum member at [src/NetPace.Core/Profile.cs](src/NetPace.Core/Profile.cs) to the full text required by FR-021: must contain the word "undocumented" (case-insensitive), explicitly name `5000`, `6000`, and `7000`, warn that future OoklaServer releases may break it, and cross-reference `docs/architecture/download-upload-size-controls.md`. Reuse the model text from `contracts/profile-enum.md`.
- [X] T022 [P] [US3] Add a `[Fact]` method to [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs) carrying `// SCENARIO: Mega uses bonus payloads`. Assert `new OoklaSpeedtestSettings(Profile.Mega).DownloadTest.DownloadSizes` contains `5000`, contains `6000`, and contains `7000` (three separate asserts so a single failure pinpoints the missing value).
- [X] T023 [P] [US3] Add a second `[Fact]` method to the same partial file carrying `// SCENARIO: Mega regression guard`. Make the assertion explicit-regression-style: collect the absent values into a list and fail with a message like `$"Mega.DownloadSizes is missing bonus payloads: {string.Join(',', missing)} — see contracts/ooklasettings-ctors.md"`. Functionally equivalent to T022 but with the "named missing values" failure mode required by the test-plan scenario.
- [X] T024 [P] [US3] Add a new test file [src/NetPace.Core.Tests/ProfileXmlDocTests.cs](src/NetPace.Core.Tests/ProfileXmlDocTests.cs) with a single `[Fact]` carrying `// SCENARIO: Mega's bonus-payload dependency is documented`. Load `NetPace.Core.xml` from the test-bin output directory (the file is generated next to the assembly when `GenerateDocumentationFile` is enabled); parse the XML; locate the `<member name="F:NetPace.Core.Profile.Mega">` node; assert the summary text contains "undocumented" (case-insensitive), `"5000"`, `"6000"`, `"7000"`, and `"download-upload-size-controls"`. Ensure `NetPace.Core.csproj` has `GenerateDocumentationFile=true` (it likely already does — verify and only edit if missing).

**Checkpoint**: Mega's bonus-payload dependency is technically present, structurally guarded against silent demotion, and explicitly documented.

---

## Phase 6: User Story 4 — Choose a profile, then override one cap (Priority: P2)

**Goal**: `--profile X --downloadsize N` / `--profile X --uploadsize N` overrides only the relevant cap via `with`-expression; the profile remains authoritative for per-request shape. `--no-download` / `--no-upload` continue to short-circuit phases regardless of profile.

**Independent Test**: `netpace --profile tiny --downloadsize 5` produces a settings record with Tiny's `DownloadSizes`/iterations/parallel and `DownloadSizeMb == 5`. `netpace --no-download --profile large` short-circuits download.

- [X] T025 [P] [US4] Add a `[Fact]` method to [src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs](src/NetPace.Console.Tests/NetPaceConsoleTests.Profile.cs) carrying `// SCENARIO: --downloadsize overrides only the cap, profile shape is preserved`. Invoke the CLI with `--profile tiny --downloadsize 5`; assert `settings.DownloadTest.DownloadSizes == [350]`, `DownloadSizeIterations == 1`, `DownloadParallelTasks == 1`, and `DownloadSizeMb == 5` (override applied).
- [X] T026 [P] [US4] Add a `[Fact]` method to the same file carrying `// SCENARIO: --uploadsize overrides only the upload cap`. Invoke the CLI with `--profile small --uploadsize 1`; assert `settings.UploadTest.UploadSizeIncrementKb == 100`, `UploadIncrements == 4`, `UploadSizeIterations == 2`, `UploadParallelTasks == 2`, and `UploadSizeMb == 1`.
- [X] T027 [P] [US4] Add a `[Fact]` method to the same file carrying `// SCENARIO: Override cap larger than natural transfer is a no-op backstop`. Invoke the CLI with `--profile tiny --downloadsize 5000`; assert `settings.DownloadTest.DownloadSizeMb == 5000` (override mechanically present on the record). In a comment, record that the natural-transfer ≤ cap so the runtime cap-check never trips — verified by Tiny's natural-budget assertion in T015, not re-tested here (no Docker integration test per D8).
- [X] T028 [P] [US4] Add a `[Fact]` method to the same file carrying `// SCENARIO: --no-download short-circuits regardless of profile`. Invoke the CLI with `--no-download --profile large`; assert the resulting run reports zero bytes transferred for the download phase (use the existing `--no-download` test pattern in `NetPaceConsoleTests.cs` as a template); assert `settings.UploadTest.UploadSizeIncrementKb == 500` (Large's value) and `UploadParallelTasks == 16` (Large's value).

**Checkpoint**: Profile-shape authority + cap-override interaction verified across both directions and both override mechanisms (`--downloadsize`/`--uploadsize` and `--no-download`/`--no-upload`).

---

## Phase 7: User Story 5 — Library consumer uses Profile from NetPace.Core (Priority: P2)

**Goal**: NuGet consumers can construct settings directly from `Profile` without going through the CLI; `Profile` itself is provider-agnostic; `with`-expression composition works cleanly; invalid `Profile` values fail loudly.

**Independent Test**: All three US5 scenarios are covered by foundational tests written in T007 (Profile-location structural) and T008 (`with`-expression composition; invalid-profile-throws). This phase verifies traceability comments are correctly attached and adds any test that is missing.

- [X] T029 [US5] Verify the test method in T007's [src/NetPace.Core.Tests/ProfileTests.cs](src/NetPace.Core.Tests/ProfileTests.cs) that asserts namespace, no-provider-extension-methods, no-helper-class, and file-existence carries the exact `// SCENARIO: Profile enum is provider-agnostic and at the root of NetPace.Core` comment. If T007 split the assertions across multiple methods, attach the comment to whichever method asserts the namespace + file-existence (the most representative). If absent, add it.
- [X] T030 [US5] Verify the `with`-expression composition test method in T008's [src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs](src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs) carries the exact `// SCENARIO: \`with\` expression composes cleanly on profile-built record` comment (note the backticks in the scenario name — preserve them character-for-character). Strengthen the assertions if T008 only covered `UseProxy`: also assert `s.DownloadTest.DownloadSizes` contains `5000`, `6000`, `7000`, `s.DownloadTest.DownloadParallelTasks == 32`, and `s.UploadTest.UploadSizeIncrementKb == 1024` (Mega's values per data-model.md).
- [X] T031 [US5] Verify the invalid-profile-throws test method in T008 carries the exact `// SCENARIO: Construct invalid profile throws` comment. Assertion must check both that `ArgumentOutOfRangeException` is thrown and that `ParamName == "profile"`.

**Checkpoint**: All 16 spec scenarios have a labelled test with a matching `// SCENARIO:` comment. `/speckit.testchecklist` (if run) should report 0 untraced scenarios.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: User-facing documentation, the Change Intent Record, and final-build verification. These touch shared files (`README.md`, `USER_GUIDE.md`, the architecture doc) but are content-additive, so independent `[P]` writers can work in parallel as long as they target different files.

- [X] T032 [P] Update [README.md](README.md): refresh the `--help` snapshot block, add a `--profile` row to the options reference table (with the five enum values and `Medium` default), and add a one-line usage example (`netpace --profile tiny`).
- [X] T033 [P] Add a "Choosing a profile" section to [USER_GUIDE.md](USER_GUIDE.md) including the budget table from data-model.md and decision guidance (cellular → Small; fibre → Large; 10 Gbps DC → Mega). Include a dedicated warning callout for `Mega` mirroring the XML doc text from T021.
- [X] T034 [P] Add a new cross-reference section to [docs/architecture/download-upload-size-controls.md](docs/architecture/download-upload-size-controls.md) mapping each profile to its per-request payload sizes (using the data-model.md tables). Explicitly note that `Mega` is the only profile relying on the bonus `5000/6000/7000` payloads, and that the documented fallback strategy (revert to historic-10 with higher iterations) is tracked but not implemented in this change.
- [X] T035 Create a new Change Intent Record at `docs/change-intent-records/CIR-NNN-profile-cli-switch.md` (replace `NNN` with the next sequential CIR number after scanning the directory). Document: (a) the public API addition — `Profile` enum, two new `OoklaSpeedtestSettings` constructors; (b) rationale for placing `Profile` in `NetPace.Core` rather than under `Clients/Ookla/`; (c) the dependency direction — provider knows `Profile`, `Profile` knows no provider, entire mapping inline; (d) the move of `DownloadSizeMb`/`UploadSizeMb` from method parameters into `DownloadTestSettings`/`UploadTestSettings`, the deletion of the corresponding overloads on `ISpeedTestService` and `OoklaSpeedtest`, and that this is an accepted pre-1.0 breaking change to the public NuGet contract. Mirror the structure of existing CIRs already in `docs/change-intent-records/`.
- [X] T036 Run final verification: `dotnet build` is warning-free; `dotnet test` is green across all three test projects (`NetPace.Core.Tests`, `NetPace.Console.Tests`, plus the legacy `NetPace.Tests` if still present); the PR title carries a breaking-change marker (e.g. `feat!:` or explicit `BREAKING CHANGE:` body line) so the auto-generated release notes pick it up.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no dependencies.
- **Phase 2 (Foundational)**: depends on Phase 1. Blocks all user-story phases.
- **Phases 3–7 (User Stories)**: each depends on Phase 2 completing. After Phase 2 they are mostly independent — different developers can pick up different user stories — except that Phases 4, 6 add tests to `NetPaceConsoleTests.Profile.cs` (created in T012); Phases 3, 5 add tests to `OoklaSpeedtestSettingsTests.Profiles.cs` (created in T009). Coordinate within file or merge in any order — the partial-class split absorbs concurrent edits naturally.
- **Phase 8 (Polish)**: depends on Phases 3–7 being complete (so the documented behaviour matches the implemented behaviour).

### User Story Dependencies

- **US1 (P1)**: depends on Phase 2 only. Independently shippable as MVP.
- **US2 (P1)**: depends on Phase 2 only. Independently shippable.
- **US3 (P2)**: depends on Phase 2 only. Mega's XML doc expansion in T021 is the one piece of US3-specific *implementation*; the rest is tests.
- **US4 (P2)**: depends on Phase 2 only. Override interaction is fully implemented in T013; this phase only adds the acceptance tests.
- **US5 (P2)**: depends on T007 and T008 (sub-tasks of Phase 2). Phase 7 itself only verifies / strengthens scenario-comment traceability.

### Within Each User Story

- Tests are written first (TDD per constitution I) — write the test, run it, see it RED, then implement (where there's anything to implement beyond the foundational switch arms).
- For US1, US2, US4: there is no per-story implementation beyond the tests — the switch arms are foundational. The TDD cycle for each scenario is: write test → see GREEN immediately if the foundational implementation is correct, or RED → fix the relevant switch arm in T010 if a value is wrong.
- For US3: T021 (XML doc expansion) and T024 (XML reflection test) form a TDD pair — write T024 first, watch it fail because the doc text doesn't yet contain the required substrings, then expand the doc in T021.

### Parallel Opportunities

- All `[P]` tasks within a phase target different files and have no dependencies on incomplete tasks.
- Phase 2 has 6 parallel tasks (T002, T003, T004, T007, T008, T009) plus three sequential ones (T005 → T006 → T010 → T013, due to compile-graph dependencies).
- Phases 3 and 4 can run in parallel after Phase 2.
- Phases 5, 6, 7 can run in parallel after Phase 2.
- Phase 8's four `[P]` tasks (T032, T033, T034 are different files; T035 is a new file — also `[P]`-compatible) can run in parallel; T036 is sequential at the end.

---

## Parallel Example: Phase 2 Foundational

```bash
# These tasks edit different files with no compile-graph dependencies — run together:
Task: T002 — Create src/NetPace.Core/Profile.cs
Task: T003 — Add DownloadSizeMb to DownloadTestSettings.cs
Task: T004 — Add UploadSizeMb to UploadTestSettings.cs
Task: T007 — Write src/NetPace.Core.Tests/ProfileTests.cs
Task: T008 — Write src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.cs
Task: T009 — Write src/NetPace.Core.Tests/OoklaSpeedtestSettingsTests.Profiles.cs

# Then T005 → T006 → T010 must run sequentially (each depends on the previous compiling):
Task: T005 — Delete int sizeMb overloads on ISpeedTestService.cs
Task: T006 — Delete matching overloads on OoklaSpeedtest.cs; rewire cap-read
Task: T010 — Implement OoklaSpeedtestSettings ctors with inline switch

# T011, T013 follow:
Task: T011 — Add Profile property to SpeedTestCommandSettings.cs
Task: T013 — Wire Option<Profile> in Program.cs
Task: T014 — Refresh --help Verify snapshot
```

## Parallel Example: Phases 3 + 5

```bash
# After Phase 2 is complete, US1 and US3 can run side-by-side:
Task: T015 [US1] — Tiny budget test
Task: T016 [US1] — Small cellular test
Task: T017 [US1] — Profile-authoritative CLI test
# In parallel:
Task: T021 [US3] — Mega XML doc expansion
Task: T022 [US3] — Mega bonus-payloads test
Task: T023 [US3] — Mega regression guard
Task: T024 [US3] — Mega XML doc reflection test
```

---

## Implementation Strategy

### MVP Scope (User Story 1 only)

1. Complete Phase 1 (Setup) — T001.
2. Complete Phase 2 (Foundational) — T002 through T014. This gives you the full `Profile` enum, all five switch arms, the CLI flag, and cross-story invariant tests.
3. Complete Phase 3 (US1) — T015, T016, T017.
4. STOP and validate: run `netpace --profile tiny` against a reachable Ookla server. Confirm reported bytes ≤ 1 MiB. Demo to the constrained-plan user persona.
5. Ship as MVP if ready — Tiny and Small users immediately benefit; Medium becomes the new default automatically.

Note: because Phase 2 implements all five profile arms (one switch expression), Medium / Large / Mega values are present and correct at MVP. They just aren't yet covered by their own acceptance tests until Phases 4–6.

### Incremental Delivery

1. Setup + Foundational → public API surface and CLI flag live, defaults shifted to Medium.
2. + US1 (Phase 3) → constrained-plan users are first-class. **MVP shippable.**
3. + US2 (Phase 4) → default-traffic-reduction is formally verified. Ship.
4. + US3 (Phase 5) → Mega users get the documented bonus-payload path. Ship.
5. + US4 (Phase 6) → cap-override interaction is formally verified. Ship.
6. + US5 (Phase 7) → traceability checkpoints close. Ship.
7. + Polish (Phase 8) → docs and CIR land. Final PR review.

### Parallel Team Strategy

If multiple developers are available after Phase 2:

- Dev A: US1 (Phase 3) → US2 (Phase 4)
- Dev B: US3 (Phase 5)
- Dev C: US4 (Phase 6)
- Dev D: US5 (Phase 7) + Phase 8 docs

US3 has the most isolated work (touches only Profile.cs's XML doc + new test file); good first-pick for a parallel developer.

---

## Notes

- `[P]` = different files, no dependencies on incomplete tasks.
- `[Story]` label maps task to a user story; no Story label on Setup / Foundational / Polish tasks.
- Per the constitution, every public API addition is added test-first. The Phase-2 foundational tests (T007, T008, T009) cover the cross-story invariants; the per-user-story tests (Phases 3–7) cover the scenario-specific framings.
- Every test method that implements a spec scenario MUST carry a `// SCENARIO: <name>` comment matching `test-plan.md` character-for-character — verified by `/speckit.testchecklist`.
- The 5-profile inline switch is implemented atomically in T010. Per-user-story phases do not edit T010's switch beyond what's listed (US3's only impl task is T021 — Mega's XML doc, not its switch arm).
- Commit at each Phase checkpoint (after Phase 2, after each user-story phase) — keeps the bisect graph clean.
- The PR title MUST flag the breaking change to `ISpeedTestService` (deletion of four `int sizeMb` overloads). There is no `CHANGELOG.md` to maintain — release notes auto-generate from PR titles.
