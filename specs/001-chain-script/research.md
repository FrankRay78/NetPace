# Research: SDLC Command Chain

Phase 0 output for [plan.md](plan.md). Facts marked **probed** were confirmed against the installed CLI (Claude Code 2.1.270) on 2026-09-13.

## R1 — How a stage is started

- **Decision**: each stage is one `claude -p "<command> <args>"` process, with the prompt as the positional immediately after `-p`, `--model "$CHAIN_MODEL"`, `--output-format json`, `--dangerously-skip-permissions`, and stdin redirected from `/dev/null`.
- **Rationale**: a separate process per stage is the confirmed decision on #270 and is what makes the ordering enforceable from outside the model. `--dangerously-skip-permissions` is how the WSL sandbox already runs the agent; without a bypass mode the repo's `acceptEdits` default silently denies every Bash call not on the allow list. Prompt-first and `</dev/null` are the two argument details `scripts/plugin-report.sh` records as learned the hard way.

## R2 — Reading a stage's verdict

- **Decision**: a stage fails if `claude` exits non-zero (a timeout included) or its report (`.result` in the JSON reply) contains no success verdict. Otherwise it succeeded. The verdict is searched for anywhere in the report:

  | Stage | Success |
  | --- | --- |
  | `/build` | `READY branch=` |
  | `/study` | `STUDIED issue=` |
  | `/verify` | `VERIFIED branch=` |
  | `/raise-pr` | a `https://github.com/…/pull/<n>` URL |

  When a failed stage's report contains `FAILED reason=<…>`, that reason is what the chain prints. A report containing both a success and a `FAILED` verdict is treated as failed.
- **Rationale**: **probed** — the JSON reply carries `result` and `session_id`. The tokens are each command's own contract, so nothing can drift. Searching the whole report is the prior-art sketch's lesson: the verdict is not reliably the last line. The `/raise-pr` rule is the confirmed decision on #270.

## R3 — Giving a study pass the preceding stage's context

- **Decision**: the chain keeps the `session_id` from the `/build` and `/verify` replies in shell variables for the run, and starts each study pass with `--resume <that id>`. The id is never printed or written.
- **Rationale**: chosen by the author during planning — `--continue` would pick up an interactive session used in the same repo during the run. **Probed** — `-p --resume <id>` answers from the earlier conversation and keeps the same `session_id`.

## R4 — Nothing carried from `/verify` to `/raise-pr`

- **Decision**: `/raise-pr <N>` runs in a fresh session against the branch.
- **Rationale**: the author's decision during planning. Findings `/verify` defers as out of scope stay visible in its report, which the chain prints as the stage ends.

## R5 — Stalled stages

- **Decision**: each stage runs under `timeout --kill-after=60 <limit>`. Fixed per-stage limits in the script: `/build` 2h, `/study` 30m, `/verify` 90m, `/raise-pr` 30m. One override, `CHAIN_STAGE_TIMEOUT` (seconds), replaces all four — for tuning and so the test can force a stall. A timeout exit is reported as "stalled".
- **Rationale**: the issue asks for per-stage limits, started conservative and tuned from real runs. One override is the least that makes the stall testable.

## R6 — Preconditions

- **Decision**: before any stage, in order: exactly one issue argument (`270` or `#270`); `git status --porcelain` empty; current branch is `main`. The first failure prints what was wrong and exits 1.
- **Rationale**: this is exactly what the issue's acceptance criterion asks for. `/build`'s own step 1 already checks the fetch and unpushed commits, and a missing tool or signed-out `gh` makes the first stage fail immediately — re-checking those in the chain buys little.

## R7 — Report mode

- **Decision**: `--dry-run` validates the argument, prints the five stages with the command each would send, and exits 0. It runs no git or `claude` command.
- **Rationale**: "no change of any kind" is guaranteed most simply by reaching no tool that could make one.

## R8 — No checks between stages

- **Decision**: the chain runs no git checks of its own between stages — not a clean-tree check, not a branch check, not a study-record comparison, not a remote check before `/raise-pr`.
- **Rationale**: each would re-check a stage's own contract (committed and clean on exit; `/study` append-only; nothing pushed before `/raise-pr`), which the issue itself says the chain should not restate. A stage that breaks its contract is caught by the next stage's own preconditions — `/verify` refuses a dirty tree — and that failure stops the chain. Cut during planning as over-engineering.

## R9 — Output and exit codes

- **Decision**: progress lines prefixed `chain:`; each stage's report printed when it ends; on failure a closing line naming the stage, its position and the reason, plus how to reopen the session. Exit 0 on a raised pull request or a dry run; exit 1 on anything else. An invalid argument prints usage.
- **Rationale**: FR-007 and SC-002 need the closing line; distinguishing kinds of failure by exit code serves only a future dispatcher (#266) that does not exist yet.

## R10 — Model

- **Decision**: `CHAIN_MODEL="${CHAIN_MODEL:-claude-opus-5}"` at the top of the script, passed to every stage.
- **Rationale**: the confirmed decision on #270.

## R11 — How the chain is tested

- **Decision**: `scripts/chain.tests.sh`, a standalone matrix in the shape of `.claude/hooks/*.tests.sh`, of about six cases. It runs the chain in a throwaway git repo with a small stub `claude` first on PATH. The stub logs each invocation's command and `--resume` id, and replies with canned JSON chosen by the test — or sleeps, for the stall case. No `gh` stub is needed: the chain never calls `gh`. Run by hand after any edit to the chain; not in CI, matching the hook matrices.

  Automated: *One issue to a pull request* (order, resumed sessions, success), *A failing stage stops the run*, *A stage with no readable verdict is a failure*, *A stalled stage ends the run*, *Wrong starting point is refused* (no issue, dirty tree, not `main`), *Asked what it would do*.

  By hand: *A failed stage can be reopened*, and the real end-to-end run — both need real sessions and a real model, and faking them would take elaborate stubs.
- **Rationale**: the gating is the one thing the chain exists to get right, and those six cases prove it with one trivial stub. The author's rule: anything needing elaborate stubs is checked by hand instead.
- **Traceability**: each case carries a `# // SCENARIO: <label>` comment — a valid bash comment containing the literal `// SCENARIO:` text `/speckit.testchecklist` matches on. The checklist finds test files by C#/TS/Python name patterns, so it has to be pointed at `scripts/chain.tests.sh`.

## Found in passing

- **Probed** — the JSON reply also carries `permission_denials`. The confirmed decision on #270 keeps silently denied `ask` rules as a documented residual risk; the field is noted in case that decision is revisited.
