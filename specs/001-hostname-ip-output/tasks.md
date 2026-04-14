# Tasks: Add Hostname and IP Address to Structured Output

**Feature**: specs/001-hostname-ip-output  
**Input**: Design documents from `specs/001-hostname-ip-output/`  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

**TDD Required**: Yes — tests must be written and confirmed failing (RED) before implementation (GREEN). This is non-negotiable per project constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are specified in each task description

---

## Phase 1: Setup

**Purpose**: Confirm baseline before starting

- [ ] T001 Verify dotnet build and dotnet test pass cleanly in the repository root before starting

**Checkpoint**: Build is green — implementation can begin

---

## Phase 2: Foundational — IClientInfoProvider Abstraction

**Purpose**: Create the `IClientInfoProvider` abstraction and propagate the `IConsoleWriter` signature change to all four writers. **Must be complete and compiling before any user story work begins.**

**⚠️ CRITICAL**: All four user story phases (JSON, CSV, and both output regression tests) depend on the `IClientInfoProvider` interface and the updated `IConsoleWriter` signature being in place.

> **NOTE: Write T002 first and confirm it FAILS before implementing T003**

- [ ] T002 Write failing unit tests for `ClientInfoProvider` covering: `GetIPAddress` returns first IPv4, falls back to first IPv6, returns empty string when no interfaces; `GetHostname` returns hostname, returns empty string when hostname is empty — 5 test methods total in `tests/NetPace.Console.Tests/ClientInfoProviderTests.cs`
- [ ] T003 Create `src/NetPace.Console/IClientInfoProvider.cs` with: `IClientInfoProvider` interface (XML docs on interface and both methods), `ClientInfoProvider` sealed production implementation (IPv4→IPv6→empty→ERROR logic using `NetworkInterface.GetAllNetworkInterfaces()` and `Dns.GetHostName()`; all exceptions caught), `ClientInfoProviderStub` with configurable `IPAddress = "192.168.1.1"` and `Hostname = "test-host"`, and `ExceptionThrowingClientInfoProviderStub` returning `"ERROR"` for both fields
- [ ] T004 Update `IConsoleWriter.PerformSpeedTestAsync` signature to add `IClientInfoProvider clientInfoProvider` parameter after `IClock clock` and before `ISpeedTestService speedTestClient` in `src/NetPace.Console/IConsoleWriter.cs`
- [ ] T005 [P] Update `DefaultConsoleWriter.PerformSpeedTestAsync` to accept the new `IClientInfoProvider clientInfoProvider` parameter (parameter is accepted but not used) in `src/NetPace.Console/ConsoleWriters/DefaultConsoleWriter.cs`
- [ ] T006 [P] Update `MinimalConsoleWriter.PerformSpeedTestAsync` to accept the new `IClientInfoProvider clientInfoProvider` parameter (parameter is accepted but not used) in `src/NetPace.Console/ConsoleWriters/MinimalConsoleWriter.cs`
- [ ] T007 Update `SpeedTestCommand` to inject `IClientInfoProvider` via constructor and pass it to all `PerformSpeedTestAsync` calls in `src/NetPace.Console/Commands/SpeedTestCommand.cs`
- [ ] T008 Register `IClientInfoProvider → ClientInfoProvider` (production path) and `IClientInfoProvider → ClientInfoProviderStub` (test path via `--test` flag) in `src/NetPace.Console/Program.cs` following the existing `IClock` registration pattern

**Checkpoint**: `dotnet build` passes and all pre-existing tests pass — user story work can now begin

---

## Phase 3: User Story 1 — JSON output includes device identifiers (Priority: P1) 🎯 MVP

**Goal**: `netpace --json` output includes `IPAddress` and `Hostname` fields after `UploadSpeed`, populated from `IClientInfoProvider`, with graceful handling of empty and error values.

**Independent Test**: Run `netpace --json` on any machine; confirm the output JSON contains `IPAddress` and `Hostname` string fields at the end of the result object.

> **NOTE: Write T009 first and confirm it FAILS before implementing T010 and T011**

- [ ] T009 [US1] Write failing JSON integration tests using `ClientInfoProviderStub` covering all 6 spec scenarios: normal (IPv4 + hostname), IPv6 fallback, empty IPAddress (`IPAddress = ""`), ERROR IPAddress (`IPAddress = "ERROR"`), ERROR Hostname (`Hostname = "ERROR"`), empty Hostname (`Hostname = ""`) — each as a separate test method with `// SCENARIO:` comment matching spec.md scenario name in `tests/NetPace.Console.Tests/NetPaceConsoleTests.Json.cs`
- [ ] T010 [US1] Add `IPAddress` (string, required) and `Hostname` (string, required) properties to `JsonResult` after `UploadSpeed` with XML documentation in `src/NetPace.Console/JsonResult.cs`
- [ ] T011 [US1] Update `JsonConsoleWriter.PerformSpeedTestAsync` to call `clientInfoProvider.GetIPAddress()` and `clientInfoProvider.GetHostname()` and populate the new `JsonResult.IPAddress` and `JsonResult.Hostname` properties in `src/NetPace.Console/ConsoleWriters/JsonConsoleWriter.cs`
- [ ] T012 [US1] Update existing JSON snapshot `.verified.txt` files in `tests/NetPace.Console.Tests/Expectations/` to include the new `IPAddress` and `Hostname` fields (run `dotnet test -- --verify-accept-snapshots` after confirming the new output is correct)

**Checkpoint**: All JSON tests pass including snapshots — US1 is fully functional and independently testable

---

## Phase 4: User Story 2 — CSV output includes device identifiers (Priority: P2)

**Goal**: `netpace --csv` output includes `IPAddress` and `Hostname` as the last two columns in both header and data rows, with consistent empty/ERROR value representation.

**Independent Test**: Run `netpace --csv` and confirm the output contains `IPAddress` and `Hostname` column headers at the end and correct values in the data row.

> **NOTE: Write T013 first and confirm it FAILS before implementing T014 and T015**

- [ ] T013 [US2] Write failing CSV integration tests using `ClientInfoProviderStub` covering 3 spec scenarios: normal (IPv4 + hostname columns present with correct values), empty values (`IPAddress = ""` and `Hostname = ""`), ERROR Hostname (`Hostname = "ERROR"`) — each as a separate test method with `// SCENARIO:` comment in `tests/NetPace.Console.Tests/NetPaceConsoleTests.CSV.cs`
- [ ] T014 [US2] Update `CSVConsoleWriter` to append `IPAddress` and `Hostname` to the header row in both with-units and without-units modes in `src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs`
- [ ] T015 [US2] Update `CSVConsoleWriter` to append `clientInfoProvider.GetIPAddress()` and `clientInfoProvider.GetHostname()` values to each data row in `src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs`
- [ ] T016 [US2] Update existing CSV snapshot `.verified.txt` files in `tests/NetPace.Console.Tests/Expectations/` to include the new `IPAddress` and `Hostname` columns (run `dotnet test -- --verify-accept-snapshots` after confirming the new output is correct)

**Checkpoint**: All CSV tests pass including snapshots — US2 is fully functional and independently testable

---

## Phase 5: User Story 3 — Non-structured output formats are unchanged (Priority: P3)

**Goal**: Default rich terminal output and minimal output do not include hostname or IP address — no regression in existing human-readable output.

**Independent Test**: Run `netpace` (default) and `netpace --output minimal`; confirm neither output contains "IPAddress" or "Hostname" text.

**Note**: No new production code required for this story — `DefaultConsoleWriter` and `MinimalConsoleWriter` were already updated in Phase 2 to accept but ignore `IClientInfoProvider`. These tasks are regression tests only.

> **NOTE: Write T017 and T018 first and confirm they FAIL before running the suite (or confirm they pass immediately, verifying no regression was introduced)**

- [ ] T017 [P] [US3] Write regression tests verifying `DefaultConsoleWriter` output does not contain the text `"IPAddress"` or `"Hostname"` for 2 spec scenarios: default output unchanged, using `ClientInfoProviderStub` in `tests/NetPace.Console.Tests/NetPaceConsoleTests.Default.cs`
- [ ] T018 [P] [US3] Write regression tests verifying `MinimalConsoleWriter` output does not contain the text `"IPAddress"` or `"Hostname"` for 2 spec scenarios: minimal output unchanged, using `ClientInfoProviderStub` in `tests/NetPace.Console.Tests/NetPaceConsoleTests.Minimal.cs`

**Checkpoint**: All user stories are independently functional and tested

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation completeness

- [ ] T019 Run full test suite and confirm all tests pass: `dotnet build && dotnet test` — zero failures, zero warnings
- [ ] T020 [P] Verify XML documentation is complete on `IClientInfoProvider` interface, both methods, `ClientInfoProvider`, `ClientInfoProviderStub`, and the new `IPAddress`/`Hostname` properties on `JsonResult` in `src/NetPace.Console/IClientInfoProvider.cs` and `src/NetPace.Console/JsonResult.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **blocks all user stories**
- **User Stories (Phases 3–5)**: All depend on Foundational completion; can proceed in priority order (P1 → P2 → P3) or in parallel if staffed
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependency on US2 or US3
- **US2 (P2)**: Can start after Foundational — no dependency on US1 or US3
- **US3 (P3)**: Can start after Foundational — no production code needed; regression tests only

### Within Each User Story (TDD order)

1. Write tests (confirm RED)
2. Implement production code (GREEN)
3. Update snapshots (Verify)
4. Confirm all story tests pass (CHECKPOINT)

### Parallel Opportunities within Phase 2

```
T002 → T003 → T004 → T005 [P], T006 [P] → T007, T008
```

T005 and T006 can be done in parallel (different files, both depend on T004).
T007 and T008 can be done in parallel (different files, both depend on T003).

---

## Parallel Execution Examples

### Phase 2: Foundational

```
After T004 completes, launch T005 and T006 in parallel:
  Task A: Update DefaultConsoleWriter.cs (T005)
  Task B: Update MinimalConsoleWriter.cs (T006)

After T003 completes, launch T007 and T008 in parallel (once T004 also done):
  Task A: Update SpeedTestCommand.cs (T007)
  Task B: Update Program.cs (T008)
```

### Phase 5: US3 Regression Tests

```
After Phase 4 completes, launch T017 and T018 in parallel:
  Task A: Default output regression tests (T017) → tests/NetPace.Console.Tests/NetPaceConsoleTests.Default.cs
  Task B: Minimal output regression tests (T018) → tests/NetPace.Console.Tests/NetPaceConsoleTests.Minimal.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T008) — CRITICAL, blocks everything
3. Complete Phase 3: US1 JSON (T009–T012)
4. **STOP and VALIDATE**: `dotnet test` — all JSON tests pass
5. Ship or demo with JSON device identifiers working

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → JSON output complete → independent validation
3. Phase 4 (US2) → CSV output complete → independent validation
4. Phase 5 (US3) → Regression safety net confirmed
5. Phase 6 → Final polish and validation

---

## Notes

- `[P]` tasks involve different files with no incomplete task dependencies
- `[Story]` label maps each task to its user story for traceability
- Every test method must include `// SCENARIO:` comment matching the exact scenario name from spec.md
- `ClientInfoProviderStub` is the primary test double — configure `IPAddress` and `Hostname` properties per test scenario
- `ExceptionThrowingClientInfoProviderStub` returns `"ERROR"` for both fields — use for error-path integration tests
- Existing snapshots in `tests/NetPace.Console.Tests/Expectations/` will fail after Phases 3 and 4 — this is expected; review and accept with `--verify-accept-snapshots`
- Commit after each phase checkpoint to preserve incremental progress
