# Research: SDLC Command Chain

Phase 0 output for [plan.md](plan.md). Every point below was open in the Technical Context; each is now decided. Facts marked **probed** were confirmed against the installed CLI (Claude Code 2.1.270) on 2026-09-13, not assumed.

## R1 — How a stage is started

- **Decision**: each stage is one `claude -p "<command> <args>"` process, with the prompt as the positional immediately after `-p`, `--model "$CHAIN_MODEL"`, `--output-format json`, `--dangerously-skip-permissions`, and stdin redirected from `/dev/null`.
- **Rationale**: a separate process per stage is the confirmed decision on #270, and it is what makes the ordering enforceable from outside the model. `--dangerously-skip-permissions` is how this repo's WSL sandbox already runs the agent ([`docs/wsl-claude-sandbox.md`](../../docs/wsl-claude-sandbox.md)); without a bypass mode, the repo's `acceptEdits` default would silently deny every Bash call not on the allow list, and a headless stage would degrade. Prompt-first and `</dev/null` are the two argument details `scripts/plugin-report.sh` records as learned the hard way (a variadic flag eats a trailing prompt; an open stdin stalls the call).
- **Alternatives considered**: `--permission-mode bypassPermissions` — equivalent; rejected only for consistency with the sandbox doc. Streaming JSON — gives live progress, but the chain needs one final verdict per stage and printing the stage's report on completion is enough.

## R2 — Reading a stage's verdict

- **Decision**: parse the JSON reply with `jq`. A stage fails if the process exits non-zero, the output is not JSON, `is_error` is true, or `subtype` is not `success`. Otherwise the stage's own report text (`result`) is searched for that stage's existing verdict **anywhere in the text**:

  | Stage | Success | Failure |
  | --- | --- | --- |
  | `/build` | `READY branch=<branch>` | `FAILED reason=<…>` |
  | `/study` | `STUDIED issue=<N> rows=<n>` | `FAILED reason=<…>` |
  | `/verify` | `VERIFIED branch=<branch>` | `FAILED reason=<…>` |
  | `/raise-pr` | a `https://github.com/<owner>/<repo>/pull/<n>` URL | anything else |

  A `FAILED reason=` anywhere wins over a success token; the reason text is what the chain reports. No success token is a failure ("no recognisable verdict").
- **Rationale**: **probed** — the JSON reply carries `type`, `subtype`, `is_error`, `result` and `session_id`. The tokens are each command's own contract (`build.md`, `study.md`, `verify.md` final reports), so the chain cannot drift from a sentinel of its own. Searching the whole text, tolerating a surrounding backtick or list marker, is what the prior-art sketch's third failure mode demands: the verdict is not reliably the last line. The `/raise-pr` rule is the confirmed decision on #270; its prose stop messages are not a maintained contract.
- **Alternatives considered**: asking every stage to emit a chain-specific marker (rejected on #270 — duplicates an existing contract, and changing the commands is out of scope); plain-text output (loses `is_error` and the session id).

## R3 — Giving a study pass the preceding stage's context

- **Decision**: the chain keeps the `session_id` from the `/build` and `/verify` replies in shell variables for the life of the run, and starts each study pass with `--resume <that id>`. The id is never printed and never written anywhere.
- **Rationale**: chosen by the author during planning, superseding "most recent session": `--continue` picks the most recently active session in the directory, which is the wrong one whenever an interactive Claude session in the same repo is used during the run. **Probed** — a `-p --resume <id>` call answers from the earlier conversation and keeps the same `session_id`.
- **Alternatives considered**: `--continue` (the collision above, with nothing to detect it); `--session-id <uuid>` pre-assigned by the chain (works, but needs a UUID source and is no simpler than reading the id back).

## R4 — Nothing carried from `/verify` to `/raise-pr`

- **Decision**: `/raise-pr` runs in a fresh session against the branch, with the issue number as its argument. No review findings are extracted or handed over.
- **Rationale**: the author's decision during planning — `/verify` fixes its in-scope findings itself. Known consequence, recorded so it is not rediscovered: `verify.md` names Important findings on *pre-existing* code as deferred in its own report only, so those do not reach the PR description. The chain prints every stage's report as it completes, so they remain visible in the run's terminal output.
- **Alternatives considered**: continuing the verify→study session into `/raise-pr`; extracting findings from `/verify`'s prose. Both dropped with the requirement.

## R5 — Stalled stages

- **Decision**: wrap each stage in `timeout --kill-after=60 <limit>`. Exit 124 (or 137 after the kill) is reported as "stalled — exceeded <limit>". Default limits: `/build` 2h, `/study` 30m, `/verify` 90m, `/raise-pr` 30m, each overridable by `CHAIN_TIMEOUT_BUILD`, `CHAIN_TIMEOUT_STUDY`, `CHAIN_TIMEOUT_VERIFY`, `CHAIN_TIMEOUT_RAISE_PR` (seconds).
- **Rationale**: the issue's unknown says to start conservative, tune from real runs, and not use one value for all stages. Env overrides are how that tuning happens without editing the script, and they also let the test matrix force a stall in a second. `timeout(1)` is already relied on by `plugin-report.sh`.
- **Alternatives considered**: `--max-turns` (bounds turns, not wall-clock, and a stuck tool call is a single turn); no limit (violates FR-010).

## R6 — Preconditions, before any model call

- **Decision**: in order, stopping at the first failure with exit code 2: exactly one issue argument (`270` or `#270`, normalised to digits); `git`, `claude`, `gh`, `jq`, `timeout` on PATH; inside a git work tree; `gh auth status` succeeds; `gh repo view` yields `<owner>/<repo>`; working tree clean; HEAD is `main`; `git fetch origin main` succeeds; `git log origin/main..main` is empty.
- **Rationale**: FR-006 and the review commentary on #270 — `/build`'s own step 1 checks the last four, but only after a model session has started. Re-checking them in bash is the only way to refuse before spending anything. `gh auth status` and `gh repo view` cost no model time and catch the missing-login edge case up front; the owner/repo is needed for the `/raise-pr` URL check.
- **Alternatives considered**: leaving all checks to `/build` (fails SC-003: a model session starts before the refusal).

## R7 — Report mode

- **Decision**: `--dry-run` validates the argument, then prints the five stages in order with the command each would run, and exits 0. It runs no git, `gh` or `claude` command at all — not even `git fetch`, which updates remote-tracking refs.
- **Rationale**: FR-016 says "no change of any kind". The simplest way to guarantee that is to reach no tool that could make one.
- **Alternatives considered**: a read-only precondition report inside dry-run — useful, but it needs `fetch` to be meaningful, and nobody asked for it.

## R8 — Checks between stages

- **Decision**:
  - After **every** stage: `git status --porcelain` is empty, otherwise the stage that just ran is failed with "left the working tree dirty" (FR-011, FR-013).
  - After `/build`: the reported branch is checked out and is not `main`; it is held as `<branch>` for the rest of the run.
  - After `/verify` and each study pass: HEAD is still `<branch>`.
  - Around the second study pass: the study record's lines before the pass must all still be present after it; any missing line fails the pass (FR-014). No record before the pass means nothing to compare.
  - Before `/raise-pr`: `git ls-remote --exit-code --heads origin <branch>` must report the branch absent (exit 2). Present means an earlier stage pushed (FR-015); any other exit means the check could not run — both stop the run.
- **Rationale**: each check is one git call deciding a requirement a stage's own verdict cannot. Checking the tree after the stage that dirtied it names the right culprit, instead of letting the next stage refuse with its own confusing message.
- **Alternatives considered**: checking for an existing PR too — unnecessary, a PR cannot exist for a branch that was never pushed.

## R9 — Output and exit codes

- **Decision**: progress lines prefixed `chain:`; each stage's `result` text printed when the stage ends; on failure one closing line naming the stage, its position and the reason, plus how to reopen the session. Exit 0 = pull request raised or dry run printed; 1 = a stage failed; 2 = usage or precondition failure.
- **Rationale**: FR-007 and SC-002 — the closing message alone must name the stage and why. Distinct codes for "refused" and "failed partway" let a later dispatcher (#266) tell a cheap refusal from an expensive failure.

## R10 — Model

- **Decision**: `CHAIN_MODEL="${CHAIN_MODEL:-claude-opus-5}"` at the top of the script, passed as `--model "$CHAIN_MODEL"` to every stage.
- **Rationale**: the confirmed decision on #270 — defined in the chain, passed through `CHAIN_MODEL`, starting with a contemporary Claude 5 model. The `:-` default leaves the environment override working, which the test matrix also uses.

## R11 — How the chain is tested

- **Decision**: `scripts/chain.tests.sh`, a standalone matrix in the shape of `.claude/hooks/*.tests.sh`: a throwaway git repo with a bare `origin`, and stub `claude` and `gh` executables first on PATH. The stub `claude` records each invocation (command, `--resume` id, model) to a log and replies with canned JSON chosen per stage by the test; it can also sleep (for the stall case) or dirty the tree. Exits non-zero on any failure. Run by hand after any edit to the chain, like the hook matrices; not wired into CI, matching that precedent.
- **Rationale**: Constitution §I applies — `chain.sh` is committed executable logic, and every one of its decisions (order, gating, verdict parsing, preconditions, dry run, between-stage checks) is deterministic given the stage replies. Stubbing the model is mocking an external dependency (Constitution *Testing Standards*), not hand-rolling a stand-in for a tool that performs the check. The issue's "verification by dry run and manual use" still holds for the one thing a stub cannot prove: that the real commands, with a real model, get an issue to a PR — that is the quickstart's manual run.
- **Assertions stay on observable outcomes** (Constitution §IX): exit code, the closing message naming the stage, which stage processes were started and in what order, the resumed session, and the state of the sandbox repo and its origin. None asserts on the script's internal variables or function structure.
- **Traceability**: each case carries a `# SCENARIO: <label>` comment matching the spec. `traceability-gate.sh` scans `src/` only, so its coverage edge stays a no-op for this feature; the markers are for `/speckit-testchecklist` and human readers.
- **Alternatives considered**: bats (a new dependency the repo does not use); no automated tests, as the issue assumed (leaves every gating branch unproven, which is the failure this chain exists to prevent in the stages it runs).

## Found in passing

- **Probed** — the JSON reply also carries `permission_denials`. The confirmed decision on #270 accepts silently denied `ask` rules as a documented residual risk, and this plan keeps that decision. The field is noted here because it would make those denials visible cheaply, if that decision is ever revisited.
