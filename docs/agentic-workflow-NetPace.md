# Agentic Workflow — the NetPace delta

Companion to [agentic-workflow.md](agentic-workflow.md), the generic, stack-portable guide. This file records **only where NetPace differs** from it or makes it concrete; anything not listed here follows the generic guide as written.

## Why a generic guide plus this delta

[agentic-workflow.md](agentic-workflow.md) is deliberately **stack-neutral** — portable principles, no NetPace specifics — so it stays reusable and easy to keep current. NetPace's concrete implementation and deviations live here.

The rule that keeps the split honest: **NetPace-specific behaviour never edits the generic guide.** A change to the principles themselves belongs in `agentic-workflow.md`; NetPace's concrete reading of them belongs here.

## Platform

- **Cross-platform, developed on Windows + WSL.** NetPace targets `win`/`linux`/`osx` (`x64`/`arm64`) and is developed on Windows with a WSL sandbox for the agent (see [wsl-claude-sandbox.md](wsl-claude-sandbox.md)).
- **Both `sh` and PowerShell script variants are kept.** Spec-kit is initialised `--script sh`; the `.ps1` copies stay for Windows-native use.
- **AOT-trimmable.** Production code must stay trim/AOT-safe (reflection-heavy APIs like `Spectre.Console.Cli` were deliberately replaced). A code constraint, not a workflow one, but it limits what "implement" may reach for.

## CI

The generic "CI on PR" step **applies fully**; NetPace has the whole `.github/workflows/` set the guide's Appendix describes:

| Workflow | Trigger | Role |
|---|---|---|
| `dotnet.yml` — Build and Test | pull_request → main | the generic **CI-on-PR** gate: build + test every PR |
| `codeql.yml` — CodeQL | push/PR/weekly | security analysis (the supply-chain-hardening line — see the CIR) |
| `claude.yml` — Claude Code | `@claude` in an issue/PR comment (author-gated) | the generic **Agent review action** — this is **Review B** |
| `speckit-reviewissue.yml` — Speckit Review Issue | `review` label applied to an issue (labeller-gated), or manual dispatch | runs the pre-spec gate unattended — see below |
| `publish-nuget.yml` | tag push | publish `NetPace.Core` to NuGet |
| `release-binaries.yml` | tag push | cross-platform binary release matrix |

**Review B is live:** `/raise-pr` requests the `@claude` review on the raised PR and never waits on it, so it sits outside the verify gate. A human reads it at merge; `capture-learnings` can fold it in later.

**The pre-spec gate runs in CI too.** `/speckit.reviewissue` (step 2) is the one step whose cost is the wait, because it grounds itself in the codebase — so labelling an issue `review` runs it unattended and leaves the gap analysis as a comment. The workflow applies [`.claude/commands/speckit.reviewissue.md`](../.claude/commands/speckit.reviewissue.md), keeping the analysis single-sourced. `/speckit.confirmissue` finds a CI-posted review by sentinel like any other, folds the answers into the issue body, then deletes the comment.

**The labels are the state** — the generic *Prefer the tracker's own state* rule, made concrete. A green run always removes the `review` label, and `/speckit.confirmissue` deletes the review comment once the decisions are in the body. So:

- **`review` labelled** — a review is pending; if it stays labelled, the run did not complete.
- **unlabelled, with a `<!-- speckit:review -->` comment** — a review is posted and awaiting answers.
- **`ready`, with a `## Confirmed decisions` section** — the gate is finished. `ready` takes precedence: an issue confirmed before this change carries both markers and is finished, not waiting.

There is no failure comment: GitHub's failed-run notification is the alert. The workflow checks both post-conditions itself rather than trusting the agent's narration, because the action exits green whenever the model finishes its turn. Labelling an issue that is `ready`, or already has a review, posts nothing and just clears the label; to re-review a confirmed issue, remove `ready` first. Refining a review in place (step 6 of the command) stays local. One gap: a label applied by any account other than the gated one creates no run at all, so the issue stays labelled but silent. Rationale and residuals: CIRs [`2026-09-07-automated-prespec-review`](change-intent-records/2026-09-07-automated-prespec-review.md) and [`2026-09-11-confirmed-decisions-replace-the-review`](change-intent-records/2026-09-11-confirmed-decisions-replace-the-review.md).

## Release pipeline

NetPace ships `NetPace.Core` as a **NuGet package** and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md). Touching `release-binaries.yml` (or other release-pipeline scope) without updating it is a documented no-no (memory: `feedback_release_pipeline_doc`). Release notes are GitHub-auto-generated from merged PRs; there is no `CHANGELOG.md`.

## Decision ledger: Change-Intent-Records **and** memory

Where the generic guide offers "Change-Intent-Records (or an equivalent decision ledger)", NetPace uses **both, for different jobs**:

- **Change-Intent-Records** — dated `YYYY-MM-DD-slug.md` files in [`docs/change-intent-records/`](change-intent-records/): the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the speckit-file guard, supply-chain hardening). When to write one: [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`: agent-facing facts and corrections, one per file. "Prefer a gate to a memory entry" is live here: several memories survive only as the *rationale* for a gate that now enforces them (`feedback_dotnet_test_no_build` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer as NetPace wires it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), are wired in `.claude/settings.json`, and are documented in [`.claude/hooks/README.md`](../.claude/hooks/README.md).

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when a `*.cs` under `src/` is newer than the built assembly | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks commits reintroducing the skip family (incl. xUnit-v3 `SkipUnless=`/`SkipWhen=`); `--check` mode for CI | PreToolUse(Bash) |
| Traceability gate | `traceability-gate.sh` — spec label ↔ test-plan scenario ↔ `// SCENARIO:` marker under `src/`, exact match; loop-guarded nudge, never a lock-out | Stop |
| Upstream-file guard | `permissions.deny` — one `Edit(path)` rule each on `.claude/skills/speckit-*/SKILL.md`, `.specify/templates/*.md`, `.specify/scripts/bash/*.sh` (an `Edit` rule covers every file-editing tool, Write included) | settings |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | **`/verify`'s formatting pass (step 1a) — `dotnet format style/whitespace ./src/NetPace.sln`, once per PR. Not a hook** (see below) | — |
| **Test-green gate** | **`/verify`'s suite gate (step 1b) — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |
| Fast/slow test categories | real-network integration tests live in a separate test category, excluded from the default run; the whole default suite is the completion gate | — |

Every hook is **fail-open with an announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`, `NETPACE_SKIP_TRACEABILITY_GATE=1`). In a harness edited with itself, a false block can lock out the tools that would fix it, so uncertain paths allow and the override announces itself on stderr.

Each hook is a **script with a `.tests.sh` case matrix beside it**, per the generic *Modifying the harness itself* rule 1 (which also covers "only exit 2 blocks a `PreToolUse`"). The exception is the PR pre-flight: an inline command in `settings.json`, so a red suite exits 1, not 2 — it is reported but does not block `gh pr create`. The binding gate is `/verify`'s suite run. The hook matrices and `scripts/chain.tests.sh` are not yet gated in CI (#296).

**Two generic gates do not apply.** There is **no stack-guard** (no external service stack to orchestrate) and **no UI-automation denylist** (a console CLI has no browser UI to guard).

**Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — how a CLI covers the *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

### Formatting

Formatting runs **once per PR**, as `/verify`'s formatting pass (step 1a), never on commit — the generic *Formatting is not verification* section, made concrete:

```bash
dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
```

The explicit solution argument is **required**: `dotnet format` only looks in the *current directory*, and NetPace's solution lives under `src/`. Without it the command fails.

**Why not per-commit.** Measured on this solution (84 `.cs` files, ~9,900 LOC): **21.4s** for one staged file, **29.5s** for seven. The cost is MSBuild **workspace load**, not file count, so every commit would pay ~20–30s regardless. A release cycle's drift, by contrast, is a handful of import reorderings and a couple of hundred trailing spaces on blank lines — nothing a reviewer would catch. The guide's "tens of seconds" holds even at this size, so "our solution is small enough to absorb it" does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, matching `.editorconfig`'s `end_of_line = lf` and the LF the index stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` rewrites every file it touches — no committed diff, since commit normalises back, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* the attribute needs a one-time refresh (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/build`

Follows the generic *build stage*. NetPace's specifics:

- **Branch:** `feature/<N>-<short-slug>`, cut from `origin/main`. `/raise-pr` and `/study` read the issue number from this pattern.
- **Commits:** `test: red phase for #<N> — …` for the red phase, then `Refs #<N>: …` in the imperative mood (constitution, *Git Workflow*).
- **Suite:** `dotnet build ./src && dotnet test ./src`, never `--no-build` (`green-gate.sh` denies a stale one).
- **Docs it must update** (`CLAUDE.md`'s paired rules): `///` XML docs on any new or changed public `NetPace.Core` API; the README.md `--help` snapshot and USER_GUIDE.md for a changed CLI option; `docs/RELEASING.md` for a release-pipeline change; a Change-Intent Record where the change is non-obvious.

## `/verify`

Follows the generic *verify gate*. NetPace's specifics:

- **Formatting pass (step 1a):** `dotnet format style/whitespace ./src/NetPace.sln` (see *Formatting*).
- **Suite gate (step 1b):** `dotnet build ./src && dotnet test ./src`. No external stack, so a full run is cheap.
- **Review A (step 2):** the applicable `pr-review-toolkit` reviewers plus `/review-slop`, in two waves: the five report-only reviewers together, then `pr-review-toolkit:code-simplifier`, which edits files, alone.
- **Review B:** the `@claude` action in `claude.yml` (see *CI*).

## Spec-kit

Pinned at **0.12.10.dev0** (as recorded in `.specify/init-options.json` and `.specify/integration.json`), initialised `--script sh`. Beyond the stock skills the spec route uses, this version installs `speckit-converge` (appends unbuilt work to `tasks.md` for `/speckit.implement` to finish) and `speckit-taskstoissues` (turns `tasks.md` into dependency-ordered GitHub issues); both are guarded like the rest and sit outside the standard sequence.

A `--force` re-init resets every stock skill's `disable-model-invocation` flag to `false`, so the flip must be re-applied after any upgrade; the upstream-file guard exists to stop that regression recurring (CIR: `2026-07-10-guard-speckit-files`; memory: `speckit_upgrade_procedure`). NetPace's custom `speckit.*` commands (`draftissue`, `reviewissue`, `confirmissue`, `testplan`, `testchecklist`) are authored here and untouched by upgrades; the guarded files are the hyphenated `speckit-*` skills.

## Token / context tooling

Two of the three tools are wired into config. `rtk` has `Bash(rtk …)` allow-entries in `.claude/settings.json` and a prefix-strip in `green-gate.sh`'s `strip_cmd_prefixes()`, which assumes rtk may be in play. `context-mode` has `mcp__plugin_context-mode_context-mode__*` allow-entries and an `enabledPlugins` entry. `read-once` appears in the guides but in no config. As the generic guide warns, **nothing verifies any of it is installed**, and a missing tool fails silently.

[`scripts/plugin-report.sh`](../scripts/plugin-report.sh) is that missing check: a manually-run report in four sections — `TOOLING` (each expected tool, `pr-review-toolkit` included: declared / installed / enabled / reachable), `CONFIG` (unresolvable hook and statusLine paths, dangling or duplicated allow-entries), `HOOKS` (what is registered and its per-invocation cost), and `PERFORMANCE` (live savings counters, where a tool exposes them).

```bash
bash scripts/plugin-report.sh
```

It installs nothing and changes no repo file, but it is not inert. Reading context-mode's counters means asking context-mode through an MCP tool, so it starts a headless `claude -p` — costing money and seconds, and needing the network and a logged-in CLI. `context-mode doctor` also checks the npm registry, and context-mode's CLI creates its empty storage directories if absent.

It is not a gate: no `--check` mode, no exit-code contract, and no hook, CI job or `/verify` step runs it. It is meant to be run on two boxes and diffed, so `TOOLING`, `CONFIG` and `HOOKS` carry no timestamps, absolute paths or raw millisecond figures. `PERFORMANCE` is exempt (its counters move every session), so cross-box diffs use the other three. A probe that cannot reach a verdict reports `unknown`, per the generic guide.

[`/install-harness-tooling`](../.claude/commands/install-harness-tooling.md) is the other half: it installs what the report finds missing. It reads each upstream `install.sh` first and **prints** the command for a human to run rather than running it, keeping the `Bash(curl:*)` / `Bash(wget:*)` denies intact. Two installers write a `PreToolUse` hook into settings themselves, so it stops at each and shows the diff — the generic rule 4 (*a human reviews each hook before it lands*) applied to installers that would otherwise wire hooks in silently.

Install status is deliberately **not** recorded in any doc: it is per-box and manual, so a written answer goes stale on the next clone. Run the report for the live answer.

## Related

- [../.specify/memory/constitution.md](../.specify/memory/constitution.md) — governance; supersedes this file and the generic guide alike.
- [RELEASING.md](RELEASING.md) — the release matrix and its contracts.
- [conventions/change-intent-records.md](conventions/change-intent-records.md) — when a change warrants a CIR; [conventions/csharp-style.md](conventions/csharp-style.md) — C# style.
- [../.claude/hooks/README.md](../.claude/hooks/README.md) — per-hook documentation.
- [study/README.md](study/README.md) — the `/study` records: what surprised a piece of work, classified by where the fix belongs, so the harness can be improved from evidence.
