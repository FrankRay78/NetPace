---

description: "Tasks for feature 002 — Windows Native AOT release artifacts"
---

# Tasks: Windows Native AOT Release Artifacts

**Input**: Design documents from `specs/002-win-aot-release/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/release-matrix.md](./contracts/release-matrix.md), [quickstart.md](./quickstart.md)

**Tests**: This feature ships **no production C# code**. The "tests" are the release-pipeline smoke gate (`NetPace.exe --version` / `--help` exit `0` on each Windows runner) and the size-assertion job — both delivered by editing the existing workflow, not by writing new test files. Spec-kit task IDs cover those workflow edits, plus the documentation updates and the rehearsal-tag verification.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently. US1 (win-x64-aot) is the MVP; US2 (win-arm64-aot) and US3 (maintainer/16-archive guarantee) follow.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps the task to a user story (US1, US2, US3) for traceability
- File paths are absolute relative to repo root.

## Path Conventions

This feature edits four files only — `.github/workflows/release-binaries.yml`, `docs/RELEASING.md`, `README.md`, `USER_GUIDE.md` — plus the verification activities in §Phase 5. **No `src/` or `tests/` files are touched.**

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm the working tree is clean and the branch is in the expected state.

- [ ] T001 Confirm current branch is `002-win-aot-release` and working tree is clean (`git status` reports no uncommitted changes outside this feature's spec/plan/tasks artefacts) before any workflow edits begin.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Workflow-shape changes shared by both Windows AOT entries. These edits to `.github/workflows/release-binaries.yml` MUST land before either US1 or US2 can produce a working AOT archive: the smoke step needs to know how to extract a `.zip`, the archive step needs to scrub `.pdb`, and the size-assertion job needs to allow `win-*` AOT through its filter. All three edit the same workflow file and must therefore proceed sequentially, not in parallel.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 Update the smoke-test step at `.github/workflows/release-binaries.yml:109-118` to handle Windows archives: branch the extraction on archive format (`unzip` for `*.zip`, existing `tar -xzf` for `*.tar.gz`), and invoke the binary as `./NetPace.exe` on Windows runners (`./NetPace` elsewhere). Keep `shell: bash` (Git Bash ships on both `windows-latest` and `windows-11-arm` per research R4).
- [ ] T003 Update the archive step at `.github/workflows/release-binaries.yml:99-107` to exclude `*.pdb` from Windows AOT zips: when `matrix.deployment == 'aot'` and `matrix.runtime` matches `win-*`, remove or omit `*.pdb` files before invoking `zip -r`. Linux/macOS branches and Windows non-AOT branches remain unchanged.
- [ ] T004 Widen the size-assertion guard at `.github/workflows/release-binaries.yml:158` from `if [ "$variant" = "aot" ] && [[ "$runtime" != linux-* ]]; then continue; fi` to allow `win-*` AOT entries through (e.g. `if [ "$variant" = "aot" ] && [[ "$runtime" != linux-* && "$runtime" != win-* ]]; then continue; fi`, or rewrite as an explicit allow-list of the four AOT-bearing RIDs). Either tactic produces the same observable behaviour per research R9.

**Checkpoint**: Foundational workflow shape is ready. US1 and US2 can now add their matrix entries and have them flow through extract → smoke → size-check correctly.

---

## Phase 3: User Story 1 - Windows x64 user gets a fast-startup AOT NetPace (Priority: P1) 🎯 MVP

**Goal**: Ship `netpace-{tag}-win-x64-aot.zip` on the next semver tag — a single-file native AOT executable that runs on Windows x64 with no .NET runtime installed.

**Independent Test**: After this phase, cut a pre-release tag (e.g. `0.6.0-rc.0`) on a branch with only the US1 changes applied (foundational + the win-x64 matrix entry + US1 doc rows). Confirm the release attaches `netpace-{tag}-win-x64-aot.zip`, that the archive contains a single `NetPace.exe`, and that `NetPace.exe --version` exits `0` on a clean Windows x64 host.

### Implementation for User Story 1

- [ ] T005 [US1] Add the `win-x64-aot` entry to `jobs.build-cross-platform.strategy.matrix.include` in `.github/workflows/release-binaries.yml`: `runtime: win-x64`, `deployment: aot`, `runs_on: windows-latest`, `publish_aot: true`, `publish_single_file: false`, `invariant_globalization: true`. Match the field shape of the existing `linux-x64-aot` entry exactly (data-model.md §Entity 1).
- [ ] T006 [P] [US1] Add `netpace-{ver}-win-x64-aot.zip` to the install table in `README.md` (alongside the existing `win-x64-standalone.zip` and `win-x64-net8.zip` rows).
- [ ] T007 [P] [US1] Update `docs/RELEASING.md` §Release matrix table: replace the `_(out of scope)_` cell at the (`win-x64`, Native AOT) intersection with `netpace-{ver}-win-x64-aot.zip`. Update the table-summary line above ("Each tag produces **14 archives**...") to reflect the new total of 16 (do this once for both Windows entries; if T013 has already been done, skip the count edit here).
- [ ] T008 [P] [US1] Add a row to `docs/RELEASING.md` §Runner per RID for `win-x64-aot` with runner `windows-latest` and rationale text matching the existing `linux-x64-aot` row's shape ("Native x64 host — no cross-compile toolchain needed; `windows-latest` ships MSVC v143 and the Windows 11 SDK pre-installed.").
- [ ] T009 [P] [US1] Update `docs/RELEASING.md` §Runner per RID prose paragraph to extend "Native AOT cannot be cross-compiled across operating systems" to explicitly cover Windows ("...hence the per-RID native runners on both Linux and Windows."). One sentence; preserve the existing wording.
- [ ] T010 [P] [US1] Update the AOT-availability note in `USER_GUIDE.md` to mention Windows alongside Linux ("AOT builds are available for Linux and Windows; macOS AOT remains a future release."). One line edit.

**Checkpoint**: At this point, the release pipeline produces a working `win-x64-aot.zip` on every tag, the size-assertion gate enforces it, and all four user-facing doc files reflect the new artefact. US1 is independently testable via a pre-release tag.

---

## Phase 4: User Story 2 - Windows ARM64 user gets a native AOT NetPace (Priority: P2)

**Goal**: Ship `netpace-{tag}-win-arm64-aot.zip` on the next semver tag — a native ARM64 executable produced on `windows-11-arm`, no x64 emulation.

**Independent Test**: After this phase, cut a pre-release tag on a branch with only the US2 changes applied (foundational + the win-arm64 matrix entry + US2 doc rows). Confirm the release attaches `netpace-{tag}-win-arm64-aot.zip`, that the archive's `NetPace.exe` reports `IMAGE_FILE_MACHINE_ARM64` in its PE header, and that the same-job smoke step on the `windows-11-arm` runner exits `0`.

### Implementation for User Story 2

- [ ] T011 [US2] Add the `win-arm64-aot` entry to `jobs.build-cross-platform.strategy.matrix.include` in `.github/workflows/release-binaries.yml`: `runtime: win-arm64`, `deployment: aot`, `runs_on: windows-11-arm`, `publish_aot: true`, `publish_single_file: false`, `invariant_globalization: true`. Above the entry, add an inline YAML comment capturing the runner-availability confirmation per FR-011 — wording suggestion: `# windows-11-arm runner is GA (April 2025) and free for public repos. Native ARM64 host preserves the same-job smoke test; cross-compile from windows-latest is rejected — see docs/RELEASING.md §Runner per RID.`
- [ ] T012 [P] [US2] Add `netpace-{ver}-win-arm64-aot.zip` to the install table in `README.md` (alongside the existing `win-arm64-standalone.zip` and `win-arm64-net8.zip` rows).
- [ ] T013 [P] [US2] Update `docs/RELEASING.md` §Release matrix table: replace the `_(out of scope)_` cell at the (`win-arm64`, Native AOT) intersection with `netpace-{ver}-win-arm64-aot.zip`. Update the table-summary line to reflect the new total of 16 archives (skip if already done in T007).
- [ ] T014 [P] [US2] Add a row to `docs/RELEASING.md` §Runner per RID for `win-arm64-aot` with runner `windows-11-arm` and rationale matching the `linux-arm64-aot` row's shape ("Native ARM64 host — AOT cross-compilation across architectures is fragile, smoke test must run natively. `windows-11-arm` runners became free for public repos in April 2025.").

**Checkpoint**: At this point, both US1 and US2 archives ship; the release matrix is at 16 entries; size-assertion enforces both new RIDs; documentation reflects both new artefacts. US2 is independently testable via a pre-release tag.

---

## Phase 5: User Story 3 - Release maintainer trusts the matrix and ships a clean tag (Priority: P3)

**Goal**: Operational verification that the extended matrix produces exactly 16 archives, that the existing 14 are unchanged, and that fail-fast behaviour halts the release if either Windows AOT smoke or size invariant is violated. This phase is **maintainer activity, not code change** — its tasks are verifications that close the loop on US1 and US2.

**Independent Test**: After Phases 3 and 4 are complete, push a release-rehearsal pre-release tag (e.g. `0.6.0-rc.0`) and walk through quickstart.md §Step 3–§Step 5. The verifier closes US3 when the rehearsal release shows 16 attached assets, the 14 pre-existing assets match a comparable post-#176 release contents-wise, and the two new Windows AOT assets pass content/size invariants.

### Verification for User Story 3

- [ ] T015 [US3] Push a release-rehearsal pre-release tag (e.g. `0.6.0-rc.0`) from the `002-win-aot-release` branch and confirm the GitHub Release for that tag has exactly 16 attached assets matching the filename list in `specs/002-win-aot-release/contracts/release-matrix.md` §Outputs (case-sensitive). Record the rehearsal-tag URL in the PR description.
- [ ] T016 [US3] Hash-compare the 14 pre-existing archives from the rehearsal release against a comparable post-#176 release of the same source state (per quickstart.md §Step 5). Confirm file lists are identical inside each archive and that binary contents differ only in the embedded version metadata. Record any discrepancies as PR-blocking findings.
- [ ] T017 [US3] Download `netpace-{rehearsal}-win-x64-aot.zip` and `netpace-{rehearsal}-win-arm64-aot.zip` from the rehearsal release and confirm each contains exactly one entry — `NetPace.exe` — with no `.dll`, `.deps.json`, `.runtimeconfig.json`, or `.pdb`. (Closes the FR-003 / contract acceptance for both new archives.)
- [ ] T018 [US3] Read the size-assertion job log on the rehearsal release and confirm both Windows AOT archives passed the new branch of the check (their sizes are strictly less than the corresponding `-standalone` archives). Closes FR-004.
- [ ] T019 [US3] (Optional but recommended) Verify fail-fast posture: on a throwaway branch, inject a forced `exit 1` into the smoke step for one of the new Windows AOT entries, push a throwaway pre-release tag, and confirm the `attach-to-release` job reports `skipped` and no assets are attached. Then revert the throwaway change. Closes the AC-3 / spec scenario "Failing Windows AOT smoke test halts release attach". If skipping for time, document the skip in the PR description.

**Checkpoint**: Maintainer trust is demonstrated — 16-archive contract holds, no regression on existing 14, size invariant enforced, fail-fast verified.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Pre-PR hygiene — confirm the build is still clean, the docs cross-reference correctly, and the PR description gives a future contributor everything they need.

- [ ] T020 Run `dotnet build` and `dotnet test` from repo root; both must exit `0` with zero warnings (project memory: "Don't commit with failing tests or build warnings"). The feature ships no source-code changes but the AOT analyzers run on every build, so this is a no-regression check on the trim/AOT clean-tree posture.
- [ ] T021 Cross-check the four edited docs (`README.md`, `docs/RELEASING.md`, `USER_GUIDE.md`, plus the workflow-comment text from T011) against `specs/002-win-aot-release/contracts/release-matrix.md`. Confirm filenames, RID names, runner names, and the 16-archive total are consistent across all five surfaces.
- [ ] T022 Open the PR for `002-win-aot-release` against `main`. PR description must reference issue #177, link to the rehearsal-tag URL recorded in T015, list all four edited files, and call out that no source-code changes were made. The `/raise-pr` skill is the standard helper; a plain `gh pr create` works equally well.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001 has no prerequisites — first task.
- **Phase 2 (Foundational)**: T002 → T003 → T004 are all edits to the same workflow file; the order between them is conventional rather than strict, but they MUST all complete before Phase 3.
- **Phase 3 (US1)**: T005 depends on Phase 2. T006–T010 depend on T005 only insofar as the matrix entry exists; doc rows can be written speculatively and merged after T005 lands.
- **Phase 4 (US2)**: T011 depends on Phase 2 (NOT on Phase 3 — US2 is independently shippable). T012–T014 same as T006–T010 relationship: doc edits hang off the existence of the matrix entry but don't depend on US1 doc edits.
- **Phase 5 (US3)**: T015–T019 require **both** Phase 3 and Phase 4 to be merged (or at least applied to the rehearsal-branch tip). The 16-archive contract spans both Windows AOT entries; verifying it requires both to exist.
- **Phase 6 (Polish)**: T020 can run any time after Phase 2; T021–T022 require the PR-ready state, i.e. all earlier phases complete.

### User Story Independence

- **US1** (T005 + T006–T010) is independently shippable: a branch with only Phase 1, Phase 2, Phase 3 changes produces a working `win-x64-aot.zip` on its own.
- **US2** (T011 + T012–T014) is independently shippable: a branch with only Phase 1, Phase 2, Phase 4 changes produces a working `win-arm64-aot.zip` on its own.
- **US3** is a verification umbrella over US1+US2; it's not independently shippable but is independently *runnable* once both have landed.

### Within Each User Story

- For US1: T005 (matrix entry) before T006–T010 (doc updates that name the new archive). T006–T010 can run in parallel — they touch different files.
- For US2: same shape — T011 before T012–T014; T012–T014 parallel.
- For US3: T015 (rehearsal-tag push) gates T016–T018 (which inspect the rehearsal-tag's assets). T019 (fail-fast verification) is independent and optional.

### Parallel Opportunities

- T006, T007, T008, T009, T010 are all marked `[P]` — different files (README.md, RELEASING.md, USER_GUIDE.md). RELEASING.md edits T007/T008/T009 touch different sections of the same file but are non-overlapping; they can be batched into a single edit pass or applied in any order.
- T012, T013, T014 marked `[P]` — same reasoning as above.
- T002/T003/T004 are NOT marked `[P]` — same file, sequential edits.

---

## Parallel Example: User Story 1 doc updates

```bash
# After T005 lands (matrix entry exists), the five doc-touching tasks can be
# applied in any order, or batched. Each one is a small, self-contained edit:
Task: "T006 Add netpace-{ver}-win-x64-aot.zip row to README.md install table"
Task: "T007 Fill (win-x64, Native AOT) cell in docs/RELEASING.md release matrix; bump 14→16 in summary line"
Task: "T008 Add win-x64-aot row to docs/RELEASING.md runner-per-RID table with windows-latest"
Task: "T009 Extend cross-OS-AOT-cannot-cross-compile prose in docs/RELEASING.md to mention Windows"
Task: "T010 Update USER_GUIDE.md AOT-availability note to mention Windows"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1 (T001) — confirm clean branch state.
2. Phase 2 (T002–T004) — apply foundational workflow shape.
3. Phase 3 (T005–T010) — add US1 matrix entry and doc rows.
4. **STOP and VALIDATE**: push pre-release tag `0.6.0-rc.0-us1`, confirm `win-x64-aot.zip` ships, archive contains only `NetPace.exe`, `--version` works, size invariant passes. (Hand-execute quickstart.md §Step 3 + §Step 4 limited to the new x64 archive.)
5. If green, the MVP is shippable. Decide whether to ship the MVP alone (drop US2/US3 to a follow-up) or proceed to US2 in the same PR.

### Incremental Delivery (recommended for this feature)

Because US1 and US2 share Phase 2's foundational changes and the matrix file lives in a single workflow YAML, shipping US1 + US2 in one PR is more efficient than splitting. Recommended order:

1. T001 → T002 → T003 → T004 (one workflow-edit pass for foundational).
2. T005 + T011 (one workflow-edit pass for both new matrix entries).
3. T006–T010, T012–T014 (doc edits in parallel — can be a single commit per file across both stories).
4. T015–T019 (rehearsal verification — shared between US1 and US2).
5. T020–T022 (polish & PR open).

### Splitting US1 and US2 into separate PRs

Possible if you want to ship US1 first and let it bake on a real release before adding US2. Cost: a second PR repeats Phase 2's verification surface (rehearsal tag, hash-compare, etc.) and the four documentation files take a second round of edits. Benefit: smaller blast radius per PR. Recommend only if `windows-11-arm` runner availability or AOT trim posture on Windows raises new concerns mid-implementation.

---

## Notes

- `[P]` tasks = different files, no dependencies. Same-file tasks (T002/T003/T004; T005/T011 if interleaved) are sequential.
- `[Story]` label maps each task to its user story for traceability with `spec.md`'s priority ordering.
- This feature ships **no production C# code** — TDD's RED-GREEN-REFACTOR cycle (Constitution principle I) does not apply at the task level. The "test" is the release-time smoke gate, which already exists in the workflow and is exercised by every AOT entry that uses it.
- Quickstart.md is the runbook for verification; tasks.md only references it. Don't duplicate its step-by-step instructions here.
- Commit after each phase or each `[P]` group; avoid mixing foundational + per-story edits in a single commit so the diff stays auditable.
