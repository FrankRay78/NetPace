# Implementation Plan: SDLC Command Chain

**Branch**: `feature/270-chain-script` | **Date**: 2026-09-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-chain-script/spec.md`

## Summary

A bash script, `scripts/chain.sh`, that carries one GitHub issue from a clean `main` to an open pull request by running `/build`, `/study`, `/verify`, `/study`, `/raise-pr` as separate headless `claude` processes. It refuses to start without an issue, on a dirty tree, or off `main`; recognises each stage's success by that command's own verdict; resumes the exact preceding session for each study pass; bounds each stage with a time limit; and stops on the first failure without touching anything. It relies on each stage's own contract rather than re-checking it. A six-case test matrix, `scripts/chain.tests.sh`, proves the gating with one small stub; a short section in `docs/agentic-workflow-NetPace.md` documents use and recovery.

## Technical Context

**Language/Version**: Bash 4+ (the WSL agent sandbox's shell)

**Primary Dependencies**: `claude` CLI (headless `-p`, JSON output, `--resume`), `git`, `jq`, coreutils `timeout` — all already required by the harness

**Storage**: N/A — the chain writes nothing

**Testing**: `scripts/chain.tests.sh` — about six cases with a stub `claude` in a throwaway repo ([research R11](research.md#r11--how-the-chain-is-tested)); the reopen scenario and a real end-to-end run by hand ([quickstart.md](quickstart.md))

**Target Platform**: Linux shell (WSL agent sandbox); bash-only by confirmed decision on #270

**Project Type**: Operator tooling script (repo harness), not part of the NetPace product

**Performance Goals**: N/A — the chain's own overhead is two git calls before the first stage

**Constraints**: No prompting; no writes; no git writes of its own; per-stage wall-clock limits

**Scale/Scope**: One issue per invocation; two small scripts and one doc section

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Verdict | How |
| --- | --- | --- |
| I. TDD | Pass | The six-case matrix is written first and watched failing before `chain.sh` exists. The reopen scenario and the real run are manual by the author's rule: anything needing elaborate stubs is checked by hand. |
| II. Library-first | N/A | Harness tooling; no NetPace product code changes. |
| III. CLI excellence | Pass (scoped) | Spectre.Console and output formats apply to the NetPace CLI. The basics that apply here are met: usage on bad arguments, actionable messages, a non-zero exit on failure. |
| IV. Cross-platform | Justified deviation | Bash-only — see Complexity Tracking. |
| V. Code quality | Pass | Follows `plugin-report.sh`'s conventions (`set -uo pipefail`, header comment on why and cost, no column alignment). |
| VI. Minimal dependencies | Pass | Nothing new. |
| VII. Semantic versioning | N/A | Not shipped. |
| VIII. AC-to-test traceability | Pass | Spec scenarios are labelled; automated cases carry `# // SCENARIO:` markers. `traceability-gate.sh` scans `src/` only, so its coverage edge is a no-op here. |
| IX. Behavioural specification | Pass | Tests assert exit codes, the closing message, which stage processes started and in what order, and repo state — never script internals. |
| X. No skipped tests | Pass | No conditional cases; a missing `git` or `jq` fails the matrix. |

**Post-design re-check**: unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/001-chain-script/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── test-plan.md
├── contracts/
│   └── chain-cli.md
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks — not created here
```

### Source Code (repository root)

```text
scripts/
├── chain.sh             # NEW — the chain
├── chain.tests.sh       # NEW — six-case matrix
└── plugin-report.sh     # existing — convention reference

docs/
└── agentic-workflow-NetPace.md   # EDIT — new "Running the chain" section after "## `/verify`"
```

**Structure Decision**: both scripts sit in `scripts/` beside `plugin-report.sh`; the matrix sits beside the script it tests, as the hook matrices do. Documentation goes in the NetPace delta doc (confirmed decision on #270), near "Permissions and unattended runs", which the chain's docs reference for the residual `ask`-rule risk.

## Departures from issue #270

Decided during planning, by the author; the issue body is updated to match:

- **Session ids are tracked in memory** for the run so a study pass resumes the right session (research R3).
- **Deferred review findings are not carried to the pull request** (research R4).
- **No checks between stages**: the chain relies on each stage's own contract for a clean tree, an append-only study record and no early push (research R8). The issue's criteria "after each study pass the working tree is clean" and "the later study pass adds to the existing record … and the invoker is told if it did not" are withdrawn as restating `/study`'s contract.
- **Preconditions are only an issue, a clean tree and `main`** (research R6); `/build` checks the rest itself.
- **A small automated matrix exists** alongside the manual runs (research R11).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
| --- | --- | --- |
| Bash-only, no PowerShell twin (Principle IV) | Confirmed decision on #270: the agent always runs from the WSL sandbox; `plugin-report.sh` sets the precedent. | A `.ps1` twin doubles the gating logic for a path no one runs. |
