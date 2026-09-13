# Implementation Plan: SDLC Command Chain

**Branch**: `feature/270-chain-script` | **Date**: 2026-09-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-chain-script/spec.md`

## Summary

A bash script, `scripts/chain.sh`, that carries one GitHub issue from a clean `main` to an open pull request by running `/build`, `/study`, `/verify`, `/study`, `/raise-pr` as separate headless `claude` processes. It refuses to start from a bad checkout, recognises each stage's success by that command's own verdict, runs a small git check between stages, stops on the first failure without touching anything, and resumes the exact preceding session for each study pass. A stubbed test matrix, `scripts/chain.tests.sh`, proves every gating branch without a model; a short section in `docs/agentic-workflow-NetPace.md` documents use and recovery.

## Technical Context

**Language/Version**: Bash 4+ (the WSL agent sandbox's shell)

**Primary Dependencies**: `claude` CLI (headless `-p`, JSON output, `--resume`), `gh`, `git`, `jq`, coreutils `timeout` — all already required by the harness; nothing new is introduced

**Storage**: N/A — no files written by the chain; reads `docs/study/<N>.md` only

**Testing**: `scripts/chain.tests.sh` — standalone matrix with stub `claude`/`gh` in a throwaway repo, the `.claude/hooks/*.tests.sh` pattern ([research R11](research.md#r11--how-the-chain-is-tested)); plus the manual runs in [quickstart.md](quickstart.md)

**Target Platform**: Linux shell (WSL agent sandbox); bash-only by confirmed decision on #270

**Project Type**: Operator tooling script (repo harness), not part of the NetPace product

**Performance Goals**: Refusal in under 10 seconds with no model call (SC-003); the chain's own overhead between stages is a handful of git calls

**Constraints**: No prompting; no writes to disk; never resets/cleans/stashes/deletes/pushes; exactly one outward-facing action (the PR); per-stage wall-clock limits

**Scale/Scope**: One issue per invocation, one invocation at a time; two new scripts (~200 and ~250 lines) and one doc section

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | How |
| --- | --- | --- |
| I. TDD | Pass | `scripts/chain.tests.sh` is written first and watched failing against a missing/empty `chain.sh`; every gating branch gets a case before its code. The real-model run is manual evidence on top, not a substitute. |
| II. Library-first | N/A | Harness tooling, not a NetPace feature; nothing in `NetPace.Core` or `NetPace.Console` changes. |
| III. CLI excellence | Pass (scoped) | Spectre.Console and output formats apply to the NetPace CLI, not operator scripts. The clig.dev basics that do apply are met: `--help`, actionable error lines, distinct exit codes, safe default. |
| IV. Cross-platform | Justified deviation | Bash-only — see Complexity Tracking. |
| V. Code quality | Pass | No build step; the script follows `plugin-report.sh`'s conventions (`set -uo pipefail`, header comment explaining why/cost, no column alignment). |
| VI. Minimal dependencies | Pass | No new dependency; every tool is already required by the harness or an existing script. |
| VII. Semantic versioning | N/A | Not shipped in a package or release. |
| VIII. AC-to-test traceability | Pass | Spec scenarios are labelled; each test case carries a matching `# SCENARIO:` comment. `traceability-gate.sh` scans `src/` only, so its coverage edge is a no-op for this feature — the link is checked by `/speckit-testchecklist`, not the gate. |
| IX. Behavioural specification | Pass | Tests assert exit codes, the closing message, which stage processes started and in what order, and sandbox repo/origin state — never script internals. |
| X. No skipped tests | Pass | The matrix fails loudly when `git` or `jq` is missing; no case is conditional. |

**Post-design re-check (after Phase 1)**: unchanged. The design adds no dependency, no product code and no platform-specific behaviour beyond the deviation below.

## Project Structure

### Documentation (this feature)

```text
specs/001-chain-script/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions R1–R11
├── data-model.md        # Phase 1 — stages, run fields, outcomes, transitions
├── quickstart.md        # Phase 1 — validation from free to expensive
├── contracts/
│   └── chain-cli.md     # Phase 1 — command line, environment, output, exit codes
├── checklists/
│   └── requirements.md  # /speckit-specify quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
scripts/
├── chain.sh             # NEW — the chain
├── chain.tests.sh       # NEW — stubbed test matrix
├── plugin-report.sh     # existing — convention reference
└── git-red-phase-commit.{sh,ps1}

docs/
└── agentic-workflow-NetPace.md   # EDIT — new "Running the chain" section, after "## `/verify`"
```

**Structure Decision**: both scripts sit in `scripts/` beside `plugin-report.sh`, the issue's named closest example of operator tooling. The test matrix lives beside the script it tests, as the hook matrices do in `.claude/hooks/`. Documentation goes in the NetPace delta doc (confirmed decision on #270) — next to `/verify` and "Permissions and unattended runs", which the chain's docs must reference for the residual `ask`-rule risk.

## Departures from issue #270

Decided during planning, by the author; the issue body is now behind on both:

- **Session ids are tracked internally** (research R3). The confirmed decision "no session IDs are captured or reported" still holds for *reporting* and *storing*; the chain holds them in memory for the run so a study pass cannot resume an unrelated interactive session.
- **Deferred review findings are not carried to the pull request** (research R4). The issue's acceptance criterion "Review findings that `/verify` deferred as out of scope appear in the pull request body" is withdrawn from the spec.
- **Automated tests exist** (research R11), where the issue's technical notes assumed dry run and manual use only — Constitution §I governs committed executable logic.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| Bash-only, no PowerShell twin (Principle IV) | Confirmed decision on #270: the agent always runs from the WSL sandbox, whatever the host OS; `plugin-report.sh` sets the precedent for bash-only operator tooling. | A `.ps1` twin doubles the gating logic to maintain and test for a path no one runs — the chain drives `claude -p` inside the sandbox. |
