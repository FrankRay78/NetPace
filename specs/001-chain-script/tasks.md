---

description: "Task list for the SDLC command chain"
---

# Tasks: SDLC Command Chain

**Input**: Design documents from `specs/001-chain-script/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/chain-cli.md, quickstart.md, test-plan.md

**Tests**: Included — Constitution §I (TDD) applies. Tests live in one matrix, `scripts/chain.tests.sh`, with one small stub `claude`. Anything needing more elaborate stubs is verified by hand (research R11).

**Organization**: One phase per user story. Almost all work is in two files, so few tasks can run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)

## Conventions for every task

- Follow `scripts/plugin-report.sh` for style: `set -uo pipefail`, a header comment explaining why the script exists and what running it costs, single-space tokens (no column alignment).
- Follow `.claude/hooks/green-gate.tests.sh` for the matrix shape: throwaway sandbox via `mktemp -d` with an `EXIT` trap, `run`/`ok` helpers, a final `RESULT: <n> passed, <m> failed` line, non-zero exit on any failure.
- Each automated test case carries a `# // SCENARIO: <name>` comment matching its `#### Scenario:` heading in `specs/001-chain-script/test-plan.md` exactly.
- Set the executable bit with `git add --chmod=+x <file>`, never `chmod` — `Bash(chmod:*)` is an `ask` rule and is silently denied in headless sessions.
- Red-phase commits: `test: red phase for #270 — <short description>`. Green commits: `Refs #270: <imperative summary>`. Never a closing keyword.
- Markdown: one line per paragraph, no hard wrapping.

---

## Phase 1: Setup

**Purpose**: The two files and the test harness every story's tests use.

- [X] T001 Create `scripts/chain.tests.sh` harness only, no test cases yet. It must: resolve the chain as `$HERE/chain.sh` and invoke it with `bash`; build a sandbox git repo per case (`git init -b main`, set a local `user.name`/`user.email`, one initial commit) and run the chain from inside it; put a stub `claude` first on PATH. The stub appends one line per invocation to a log file (its full argument list), reads the prompt (the argument after `-p`), and prints the JSON reply for that call number from files the test case writes (`reply-<n>.json`); if a `sleep-<n>` file exists it writes its PID to `pid-<n>` and sleeps that many seconds first. A helper builds a reply: `{"type":"result","subtype":"success","is_error":false,"result":<text>,"session_id":"sess-<n>"}`. Mark executable with `git add --chmod=+x scripts/chain.tests.sh`.
- [X] T002 Create `scripts/chain.sh` containing only the shebang, the header comment (what it does; that it spends substantial model time and opens a real pull request; that it never resets, cleans, pushes or writes files; prerequisites `git`, `claude`, `gh`, `jq`, `timeout`), `set -uo pipefail`, `CHAIN_MODEL="${CHAIN_MODEL:-claude-opus-5}"`, the per-stage limits (build 7200, study 1800, verify 5400, raise-pr 1800 seconds) overridden by `CHAIN_STAGE_TIMEOUT` when set, a `usage` function, and `exit 1`. Mark executable with `git add --chmod=+x scripts/chain.sh`.

**Checkpoint**: `bash scripts/chain.tests.sh` runs, reports `RESULT: 0 passed, 0 failed`, and exits 0.

---

## Phase 2: Foundational

None — the Phase 1 harness is the only shared prerequisite.

---

## Phase 3: User Story 1 — One issue to a pull request, unattended (Priority: P1) 🎯 MVP

**Goal**: A run where every stage succeeds goes build → study → verify → study → raise-pr, with each study pass resuming the right session, and ends reporting the PR.

**Independent Test**: The "One issue to a pull request" case passes against canned successful replies.

### Tests for User Story 1

- [X] T003 [US1] In `scripts/chain.tests.sh`, add case `# // SCENARIO: One issue to a pull request`. Replies: call 1 result contains `READY branch=feature/270-x`; call 2 `STUDIED issue=270 rows=0`; call 3 `VERIFIED branch=feature/270-x`; call 4 `STUDIED issue=270 rows=1`; call 5 `https://github.com/o/r/pull/9`. Run `chain.sh 270` with `CHAIN_MODEL=test-model` and stdin from `/dev/null`. Assert: exit 0; the stub log has exactly 5 lines, whose prompts are `/build 270`, `/study 270`, `/verify`, `/study 270`, `/raise-pr 270` in that order; line 2 contains `--resume sess-1` and line 4 contains `--resume sess-3`; lines 1, 3 and 5 contain no `--resume`; every line contains `--model test-model`; output contains `https://github.com/o/r/pull/9`.
- [X] T004 [US1] Run `bash scripts/chain.tests.sh`, confirm the T003 case fails, and commit the red phase.

### Implementation for User Story 1

- [X] T005 [US1] In `scripts/chain.sh`, implement the successful path: accept exactly one `<issue>` (`270` or `#270`, normalised to digits; anything else → `usage`, exit 1). A `run_stage` function takes position, name, prompt, verdict pattern, limit and an optional resume id; runs `timeout --kill-after=60 <limit> claude -p "<prompt>" --model "$CHAIN_MODEL" --output-format json --dangerously-skip-permissions [--resume <id>] </dev/null`; prints `chain: [k/5] <name> — starting`; extracts `.result` and `.session_id` with `jq`; prints the result text; succeeds when the result contains the stage's verdict (research R2 — `READY branch=`, `STUDIED issue=`, `VERIFIED branch=`, or a `https://github.com/…/pull/<n>` URL, matched anywhere in the text) and prints `chain: [k/5] <name> — ok`. Run the five stages in order, passing the build session id to stage 2 and the verify session id to stage 4. End with `chain: done — <PR URL>` and exit 0.
- [X] T006 [US1] Run `bash scripts/chain.tests.sh`, confirm it is green, and commit.

**Checkpoint**: US1 case passes.

---

## Phase 4: User Story 2 — A run that goes wrong stops safely and can be diagnosed (Priority: P1)

**Goal**: Any failed stage stops the run with a closing message naming the stage, position and reason, plus how to reopen the session.

**Independent Test**: The three automated failure cases pass; reopening a real session is checked by hand (T020).

### Tests for User Story 2

- [ ] T007 [US2] In `scripts/chain.tests.sh`, add case `# // SCENARIO: A failing stage stops the run`: calls 1–2 succeed as in T003; call 3 result contains `FAILED reason=suite red`. Record `git rev-parse HEAD`, the current branch and `git status --porcelain` in the sandbox before the run. Assert: exit 1; stub log has exactly 3 lines; output contains `verify`, `3/5` and `suite red`; output contains `claude --resume`; HEAD, branch and status are unchanged.
- [ ] T008 [US2] Add case `# // SCENARIO: A stage with no readable verdict is a failure`: call 1 result is `I finished.` (no verdict). Assert: exit 1; stub log has exactly 1 line; output names `build` and says it gave no recognisable verdict.
- [ ] T009 [US2] Add case `# // SCENARIO: A stalled stage ends the run`: `CHAIN_STAGE_TIMEOUT=1`; call 1 has `sleep-1` set to 30. Assert: exit 1; stub log has exactly 1 line; output names `build` and contains `stalled`; the PID in `pid-1` is no longer running (`! kill -0`).
- [ ] T010 [US2] Run `bash scripts/chain.tests.sh`, confirm the T007–T009 cases fail, and commit the red phase.

### Implementation for User Story 2

- [ ] T011 [US2] In `scripts/chain.sh`'s `run_stage`, handle failure: timeout exit 124 or 137 → reason `stalled — exceeded <limit>s`; any other non-zero exit → `claude exited with <code>`; result contains `FAILED reason=` → that reason text (takes precedence over a success verdict in the same text); no success verdict → `no recognisable verdict`. On failure print `chain: FAILED at [k/5] <name> — <reason>`, then `chain: no later stage ran; reopen the failed session with \`claude --resume\` (most recent headless session in this repo).`, and exit 1. The chain runs no git command that changes anything.
- [ ] T012 [US2] Run `bash scripts/chain.tests.sh`, confirm all cases are green, and commit.

**Checkpoint**: US1 and US2 cases pass.

---

## Phase 5: User Story 3 — Refused before spending anything (Priority: P2)

**Goal**: No issue, a dirty tree, or a branch other than `main` is refused before any stage starts.

**Independent Test**: The refusal case passes with zero stub invocations.

### Tests for User Story 3

- [ ] T013 [US3] In `scripts/chain.tests.sh`, add case `# // SCENARIO: Wrong starting point is refused` with three sub-checks, each in a fresh sandbox: (a) `chain.sh` with no argument; (b) an untracked file present, `chain.sh 270`; (c) a `feature/x` branch checked out, `chain.sh 270`. For each assert: exit 1; the stub log is empty or absent; output says what was wrong (usage / not clean / not on main); branch and `git status --porcelain` are unchanged.
- [ ] T014 [US3] Run `bash scripts/chain.tests.sh`, confirm the T013 sub-checks (b) and (c) fail, and commit the red phase.

### Implementation for User Story 3

- [ ] T015 [US3] In `scripts/chain.sh`, before the first stage: if `git status --porcelain` is non-empty print `chain: refused — working tree is not clean; no stage was started.` and exit 1; if `git rev-parse --abbrev-ref HEAD` is not `main` print `chain: refused — <branch> is checked out, not main; no stage was started.` and exit 1.
- [ ] T016 [US3] Run `bash scripts/chain.tests.sh`, confirm all cases are green, and commit.

---

## Phase 6: User Story 4 — Asked what it would do (Priority: P3)

**Goal**: `--dry-run` lists the stages and their commands and runs nothing.

**Independent Test**: The dry-run case passes with zero stub invocations and an unchanged sandbox.

### Tests for User Story 4

- [ ] T017 [US4] In `scripts/chain.tests.sh`, add case `# // SCENARIO: Asked what it would do`: record HEAD, branch and status; run `chain.sh --dry-run 270`. Assert: exit 0; output contains `/build 270`, `/study 270`, `/verify`, `/study 270`, `/raise-pr 270` in that order; the stub log is empty or absent; HEAD, branch and status are unchanged.
- [ ] T018 [US4] Run `bash scripts/chain.tests.sh`, confirm the T017 case fails, and commit the red phase.

### Implementation for User Story 4

- [ ] T019 [US4] In `scripts/chain.sh`, accept `--dry-run` before `<issue>`: after argument validation and before any git or `claude` command, print the dry-run listing from `specs/001-chain-script/contracts/chain-cli.md` and exit 0. Run `bash scripts/chain.tests.sh`, confirm all cases are green, and commit.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T020 [P] Add a `## Running the chain` section to `docs/agentic-workflow-NetPace.md`, directly after the `## \`/verify\`` section: what the chain runs and in what order; prerequisites (`git`, `claude`, `gh`, `jq`, `timeout`; `claude` and `gh` signed in; clean `main`); invocation and `--dry-run`; `CHAIN_MODEL` and `CHAIN_STAGE_TIMEOUT`; what it costs (substantial model time, a real PR); when a stage fails — read the closing line, reopen the session with `claude --resume`, finish the remaining stages by hand; that it relies on each stage's own contract rather than re-checking; the residual risk of silently denied `ask` rules, linking to the "Permissions and unattended runs" section; and how to run `scripts/chain.tests.sh`. One line per paragraph.
- [ ] T021 Run quickstart §1–§3 from `specs/001-chain-script/quickstart.md` (matrix, dry run, refusals — all free) against the real repository and confirm each expectation.
- [ ] T022 **Author, by hand**: run quickstart §4 (a full run against a small ready issue) and §5 (a forced stall, then reopen the session with `claude --resume`). These cover "A failed stage can be reopened" and the real-model half of "One issue to a pull request".
- [ ] T023 Run `/speckit.testchecklist`, pointing it at `scripts/chain.tests.sh`, and confirm every automated scenario in `test-plan.md` is traced.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none. T001 and T002 touch different files but the checkpoint needs both.
- **User stories (Phases 3–6)**: all depend on Setup. Every story edits the same two files, so run them one after another in priority order: US1 → US2 → US3 → US4.
- **Polish (Phase 7)**: T020 can run any time after Setup; T021–T023 need all stories complete.

### User Story Dependencies

- **US1**: none beyond Setup.
- **US2**: builds on US1's `run_stage` (T005).
- **US3**: independent of US1/US2 logic, but edits the same files — sequence after US2.
- **US4**: needs only argument parsing (T005); sequence after US3.

### Within Each User Story

- Test cases first, watched failing and committed red, then implementation, then green and committed.

### Parallel Opportunities

- T001 and T002 (different files).
- T020 (docs) alongside any script task.

---

## Parallel Example

```bash
Task: "Create scripts/chain.tests.sh harness (T001)"
Task: "Create scripts/chain.sh skeleton (T002)"

# Later, alongside any story phase:
Task: "Add Running the chain section to docs/agentic-workflow-NetPace.md (T020)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 → Phase 3.
2. **Stop and validate**: the US1 case passes; `--dry-run` does not exist yet, so do not run the chain against the real repo until US2 and US3 are in — without them a failing stage would not stop the run cleanly.

### Incremental Delivery

1. US1 → the chain runs end to end on success.
2. US2 → failures stop safely. **Minimum before any real run.**
3. US3 → bad starting points are refused for free.
4. US4 → report mode.
5. Polish → docs, free quickstart checks, the author's manual runs, traceability check.

---

## Notes

- No task adds checks between stages, precondition checks beyond issue / clean tree / `main`, or distinct exit codes — all deliberately cut (research R6, R8, R9).
- Commit after each red and green step.
