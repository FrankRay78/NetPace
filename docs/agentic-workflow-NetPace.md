# Agentic Workflow — the NetPace delta

Companion to [agentic-workflow.md](agentic-workflow.md), the generic, stack-portable guide. This file records **only where NetPace differs** from it, or makes a generic instruction concrete. If a practice is not listed here, NetPace follows the generic guide as written.

## Why a generic guide plus this delta

[agentic-workflow.md](agentic-workflow.md) is kept deliberately **stack-neutral** — the portable principles, with no NetPace specifics — so it stays reusable and easy to keep current. This file is where NetPace's concrete implementation, and any deviations from the guide, live.

The rule that keeps the split honest: **NetPace-specific behaviour never edits the generic guide** — it belongs here. When the guide's principles themselves change, that diff belongs in `agentic-workflow.md`; only NetPace's concrete reading of them belongs in this delta.

## Platform

- **Cross-platform, developed on Windows + WSL.** NetPace targets `win`/`linux`/`osx` (`x64`/`arm64`) and is developed on Windows with a WSL sandbox for the agent (see [wsl-claude-sandbox.md](wsl-claude-sandbox.md)).
- **Both `sh` and PowerShell script variants are kept.** NetPace retains the spec-kit `.ps1` variants alongside the `.sh` ones. Spec-kit is initialised `--script sh`; the PowerShell copies stay for Windows-native use.
- **AOT-trimmable.** Production code must stay trim/AOT-safe (reflection-heavy APIs like `Spectre.Console.Cli` were deliberately replaced). A code constraint rather than a workflow one, but it shapes what "implement" may reach for.

## CI

The generic guide's "CI on PR" step **applies fully**; NetPace realises the whole `.github/workflows/` set the guide's Appendix describes:

| Workflow | Trigger | Role |
|---|---|---|
| `dotnet.yml` — Build and Test | pull_request → main | the generic **CI-on-PR** gate: build + test every PR |
| `codeql.yml` — CodeQL | push/PR/weekly | security analysis (the supply-chain-hardening line — see the CIR) |
| `claude.yml` — Claude Code | `@claude` in an issue/PR comment (author-gated) | the generic **Agent review action** — this is **Review B** |
| `speckit-reviewissue.yml` — Speckit Review Issue | `review` label applied to an issue (labeller-gated), or manual dispatch | runs the pre-spec gate unattended — see below |
| `publish-nuget.yml` | tag push | publish `NetPace.Core` to NuGet |
| `release-binaries.yml` | tag push | cross-platform binary release matrix |

**Review B is live:** the `@claude` action posts on the raised PR. `/raise-pr` requests it and never waits on it, so it sits outside the verify gate. A human reads it at merge, and `capture-learnings` can fold it in later.

**The pre-spec gate runs in CI too.** Step 2 of the generic sequence (`/speckit.reviewissue`) is the one step whose cost is the wait, because it carries the codebase grounding — so labelling an issue `review` runs it unattended and leaves the gap analysis waiting as a comment. The workflow reads [`.claude/commands/speckit.reviewissue.md`](../.claude/commands/speckit.reviewissue.md) and applies it, so the analysis stays single-sourced, and `/speckit.confirmissue` folds the answered comment into the issue body — a CI-posted review needs no special handling, it is found by sentinel like any other — then deletes it.

**The labels are the state** — the generic guide's *Prefer the tracker's own state* rule, made concrete. A green run always ends with the `review` label gone, and `/speckit.confirmissue` deletes the review comment once the decisions are on the issue body — so the state reads off the two labels plus the body: **`review` labelled** means a review is pending, **unlabelled with a `<!-- speckit:review -->` comment** means a review is posted and waiting on answers, **`ready` with a `## Confirmed decisions` section** means the gate is finished, and **still `review` labelled** means the run did not complete. `ready` takes precedence: an issue confirmed before this change carries both markers and is finished, not waiting. There is no failure comment — GitHub's failed-run notification is the alert, and the workflow verifies both post-conditions itself rather than trusting the agent's narration, because the action exits green whenever the model finishes its turn. Labelling an issue that is `ready`, or that already has a review, posts nothing and just clears the label; to review a confirmed issue again, remove `ready` first. Refining a review in place (step 6 of the command) stays local. One gap worth knowing: a label applied by any account other than the gated one creates no run at all, so that case is labelled-but-silent. Rationale and residuals: CIRs [`2026-09-07-automated-prespec-review`](change-intent-records/2026-09-07-automated-prespec-review.md) and [`2026-09-11-confirmed-decisions-replace-the-review`](change-intent-records/2026-09-11-confirmed-decisions-replace-the-review.md).

## Release pipeline

NetPace ships: `NetPace.Core` as a **NuGet package**, and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md); touching `release-binaries.yml` (or any release-pipeline scope) without updating it is a documented no-no (CLAUDE.md rule). Per-release "what changed" notes are GitHub-auto-generated from merged PRs — there is no `CHANGELOG.md` to maintain.

## Decision ledger: Change-Intent-Records **and** memory

Where the generic guide offers "Change-Intent-Records (or an equivalent decision ledger)", NetPace uses **both, for different jobs**:

- **Change-Intent-Records** — [`docs/change-intent-records/`](change-intent-records/), dated `YYYY-MM-DD-slug.md` files, are the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the speckit-file guard, supply-chain hardening). When to write one is governed by [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`, holds the agent-facing facts and corrections (one fact per file). The generic guide's "prefer a gate to a memory entry" rule is live: several memories exist only as the *rationale* for a gate that now enforces them (`feedback_trusting_a_test_run` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer, as NetPace wires it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), wired in `.claude/settings.json`, each with a `.tests.sh` case matrix beside it. Documented per-hook in [`.claude/hooks/README.md`](../.claude/hooks/README.md).

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when a `*.cs` under `src/` is newer than the built assembly | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks commits reintroducing the skip family (incl. xUnit-v3 `SkipUnless=`/`SkipWhen=`); `--check` mode for CI | PreToolUse(Bash) |
| Traceability gate | `traceability-gate.sh` — spec label ↔ test-plan scenario ↔ `// SCENARIO:` marker under `src/`, exact match; loop-guarded nudge, never a lock-out | Stop |
| Upstream-file guard | `permissions.deny` — one `Edit(path)` rule each on `.claude/skills/speckit-*/SKILL.md`, `.specify/templates/*.md`, `.specify/scripts/bash/*.sh` (an `Edit` rule covers every file-editing tool, Write included) | settings |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | **`/verify`'s formatting pass (step 1a) — `dotnet format style/whitespace ./src/NetPace.sln`, once per PR. Not a hook** (see below) | — |
| **Test-green gate** | **`/verify`'s suite gate (step 1b) — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |

Every hook is **fail-open with an announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`, `NETPACE_SKIP_TRACEABILITY_GATE=1`). For a harness edited with itself, a false block can lock out the tools that would fix it — so uncertain paths allow, and the override announces itself on stderr.

Every hook is also a **script in `.claude/hooks/` with a `.tests.sh` case matrix beside it**, per the generic guide's *Modifying the harness itself* rule 1 — which is also where the "only exit 2 blocks a `PreToolUse`" caveat lives. One exception: the PR pre-flight is an inline `dotnet build ./src && dotnet test ./src` in `settings.json`, not a script, so a red suite exits 1 rather than 2 — it is reported, but does not block `gh pr create`. The binding gate is `/verify`'s suite run.

**Two generic gates do not apply here.** NetPace has **no stack-guard** — there is no external service stack to orchestrate — and **no UI-automation denylist**: it is a console CLI, not a browser UI, so a denylist has nothing to guard. NetPace's console output *is* verified — see below — just not by a browser-automation framework.

### Formatting

Formatting runs **once per PR**, as `/verify`'s formatting pass (step 1a) — never on commit. This is the generic guide's *Formatting is not verification — do it at verify cadence* section, made concrete:

```bash
dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
```

The explicit solution argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's lives under `src/`, not the repo root. Omitting it fails outright.

**Why not per-commit.** Measured on this solution (84 `.cs` files, ~9,900 LOC): **21.4s** for a single staged file, **29.5s** for a seven-file set. The cost is MSBuild **workspace load**, not file count — so staging fewer files makes it no cheaper, and every commit would pay the full ~20–30s. Against that, a release cycle's worth of drift is a handful of import reorderings and a couple of hundred trailing spaces on blank lines: nothing a reviewer would catch. The guide's "tens of seconds" figure holds even at this size, which is why the "our solution is small enough to absorb it" argument does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, agreeing with `.editorconfig`'s `end_of_line = lf` and the LF the index already stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` then rewrites every file it touches — no committed diff, since the rewrite normalises back on commit, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* that attribute needs a one-time refresh to pick it up (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/build`

`/build` follows the generic *build stage*. NetPace's specifics:

- **Branch:** `feature/<N>-<short-slug>`, cut from `origin/main`. `/raise-pr` and `/study` read the issue number from this pattern.
- **Commits:** `test: red phase for #<N> — …` for the red phase, then `Refs #<N>: …` in the imperative mood (constitution, *Git Workflow*).
- **Suite:** `dotnet build ./src && dotnet test ./src`, never `--no-build` (`green-gate.sh` denies a stale one).
- **Docs it must update** (`CLAUDE.md`'s paired rules): `///` XML docs on any new or changed public `NetPace.Core` API; the README.md `--help` snapshot and USER_GUIDE.md for a changed CLI option; `docs/RELEASING.md` for a release-pipeline change; a Change-Intent Record where the change is non-obvious.

## `/verify`

`/verify` follows the generic *verify gate*. NetPace's specifics:

- **Formatting pass (step 1a):** `dotnet format style/whitespace ./src/NetPace.sln` (see *Formatting*).
- **Suite gate (step 1b):** `dotnet build ./src && dotnet test ./src`. The suite has no external stack, so a full run costs little.
- **Review A (step 2):** the `pr-review-toolkit` reviewers that apply to the diff, plus `/review-slop`. They run in two waves: the five reviewers that only report findings run together, and `pr-review-toolkit:code-simplifier`, which edits files directly, runs alone after them.
- **Review B:** the `@claude` action in `claude.yml` (see *CI*).

## Running the chain

`scripts/chain.sh <issue>` is NetPace's implementation of the generic *Running the stages end to end*. It runs each stage as its own headless `claude -p` process. Why its last stage opens the PR without a pause is recorded in the [CIR](change-intent-records/2026-09-14-chain-raises-pr-unattended.md).

**Prerequisites.** `git`, `claude`, `gh`, `jq` and `timeout` on PATH; `claude` and `gh` signed in; a clean checkout of `main`. The chain itself checks the five tools, that an issue was named, the clean tree and `main`; `/build` checks the fetch, unpushed commits and the issue.

**Invocation.** `scripts/chain.sh 270` (or `#270`). `scripts/chain.sh --dry-run 270` lists the five stages and the command each would send, and runs nothing — no git command, no model.

**Configuration.** `CHAIN_MODEL` (default `claude-opus-5`) is the one model every stage uses. Each stage has its own time limit — build 2h, study 30m, verify 90m, raise-pr 30m — and `CHAIN_STAGE_TIMEOUT` (seconds) replaces all four, for tuning from real runs.

**What it costs.** Substantial model time — the better part of an hour for a small issue — and a real pull request on GitHub. Run `--dry-run` first if in doubt.

**When a stage fails.** The closing message names the stage, its position (`[3/5]`) and the reason: the stage's own `FAILED reason=`, `no recognisable verdict`, `claude reported an error`, `reply was not JSON`, `claude exited with <code>`, or `stalled — exceeded <n>s`. Its second line gives `claude --resume <id>` for the failed stage's session, if the reply got far enough to include an id. If no id was captured, it tells you to reopen the most recent headless session for this repo instead. Stages run under `--dangerously-skip-permissions`, so the generic guide's residual risk from silently denied `ask` rules applies.

**Tests.** `scripts/chain.tests.sh` proves the gating — order, resumed sessions, malformed and errored replies, failure, stall, refusals and dry run — against a stub `claude` in throwaway repos: no model is called and your checkout is untouched. It runs in seconds and should be run after any edit to the chain; like the hook matrices, it is not yet gated in CI (#296).

Two things the stub cannot prove need a real model, so they are checked by hand:

- **Reopening a failed stage's session.** From a clean `main`, force a stall with `CHAIN_STAGE_TIMEOUT=60 scripts/chain.sh <issue>`. Expect `chain: FAILED at [1/5] build — stalled — exceeded 60s`, exit 1, and no later stage. The closing message names the session; `claude --resume <id>` should open that stalled `/build`. Remove any branch it left by hand.
- **A full run**, the better part of an hour of model time against a small ready issue: `scripts/chain.sh <issue>` from a clean `main`. Expect five `ok` lines in order, `chain: done — <pull request URL>`, exit 0, no prompt at any point, and a clean working tree.

## Permissions and unattended runs

The mechanism — `ask` rules outranking every mode, headless runs silently denying them, and the headless run as a rule-matching oracle with its blind spot — is general Claude Code behaviour, documented in the generic guide's [*Permissions and unattended runs*](agentic-workflow.md#permissions-and-unattended-runs). What follows is NetPace's rule set, and the changes to it that it drove.

`Bash(rm:*)` and `Bash(rmdir:*)` came off the list for this reason ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)), and `Bash(git push:*)` followed them off it into `allow` — that is what lets `/raise-pr` reach its push step without stopping. `Bash(chmod:*)` moved the other way, from `deny` onto `ask`, which buys an approval path interactively but not in a lane worker, where `ask` still denies silently ([CIR](change-intent-records/2026-09-04-push-allow-chmod-ask.md)).

`permissions.deny` no longer carries `Read(…)` rules. It held six, over `.env`, `secrets.*`, `.ssh/**` and `appsettings*.json`, and any of them made a recursive read of the repo escalate to an approval no mode auto-grants — the check is glob-scope-based, not existence-based, so it fired even though the repo contains none of those files ([CIR](change-intent-records/2026-09-04-read-deny-rules-removed.md)).

## Test-green gate & categories

- The completion gate is the real suite run inside `/verify` (above), with a second run by the `gh pr create` pre-flight hook, which reports a red suite but does not block (see *The gates, concretely*) — both are `dotnet build ./src && dotnet test ./src`. There is no ledger/Stop-hook proxy (the shape the generic guide's *Where the completion gate belongs* section warns against).
- **Fast/slow split.** Real-network integration tests live in a **separate test category**, excluded from the default run, so the inner loop stays seconds-fast; the whole (default) suite is the completion gate.
- **Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — that is how a CLI covers the generic guide's *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

## Spec-kit

Pinned at **0.12.10.dev0** (what `.specify/init-options.json` and `.specify/integration.json` record), initialised `--script sh`. Beyond the stock skills the generic spec route uses, this version also installs `speckit-converge` (appends unbuilt work to `tasks.md` so `/speckit.implement` can finish it) and `speckit-taskstoissues` (turns `tasks.md` into dependency-ordered GitHub issues); both are guarded like the rest and are not part of the standard sequence. A `--force` re-init resets every stock skill's `disable-model-invocation` flag to `false` — the flip must be re-applied after any upgrade, and the upstream-file guard above exists to stop that regression recurring (CIR: `2026-07-10-guard-speckit-files`; memory: `speckit_upgrade_procedure`). NetPace's custom `speckit.*` commands (`draftissue`, `reviewissue`, `confirmissue`, `testplan`, `testchecklist`) are authored here, not stock — an upgrade does not touch them; the guarded files are the hyphenated `speckit-*` skills.

## Token / context tooling

Two of the three are wired into NetPace's config: `rtk` has `Bash(rtk …)` allow-entries in `.claude/settings.json` and a prefix-strip in `green-gate.sh`'s `strip_cmd_prefixes()` — a gate written on the assumption that rtk may be in play — while `context-mode` has a block of `mcp__plugin_context-mode_context-mode__*` allow-entries and an `enabledPlugins` entry. `read-once` is described in the guides but referenced by no config at all. As the generic guide's token-plugins section warns, **nothing verifies that any of it is installed**, and a missing tool fails silently.

[`scripts/plugin-report.sh`](../scripts/plugin-report.sh) is that missing check — a manually-run report (it installs nothing and changes no file in this repo, but it is not inert: reporting context-mode's counters truthfully means asking context-mode, whose figures live behind an MCP tool, so it starts a headless `claude -p` — that spends money, takes seconds and needs the network and a logged-in CLI; `context-mode doctor` also checks the npm registry, and context-mode's own CLI creates its empty storage directories when absent) covering four sections: `TOOLING` (expected tool → declared / installed / enabled / reachable, `pr-review-toolkit` included), `CONFIG` (unresolvable hook and statusLine paths, plus dangling or duplicated allow-entries), `HOOKS` (what is registered and what each costs per invocation), and `PERFORMANCE` (live savings counters, where a tool exposes them).

```bash
bash scripts/plugin-report.sh
```

It is not a gate: no `--check` mode, no exit-code contract, and it is wired into no hook, no CI job and no `/verify` step. The intended use is running it on two boxes and diffing — which is why `TOOLING`, `CONFIG` and `HOOKS` carry no timestamps, no absolute paths and no raw millisecond figures. `PERFORMANCE` is explicitly exempt (live counters move every session), so cross-box diffs use the other three sections. A probe it cannot reach a verdict on reports `unknown` rather than `no` — a report whose product is a truthful yes/no must not launder a failed lookup into an answer.

[`/install-harness-tooling`](../.claude/commands/install-harness-tooling.md) is the other half of the pair: it installs what the report says is missing. It reads each upstream `install.sh` before recommending it and **prints** the command for a human to run rather than executing it, so the `Bash(curl:*)` / `Bash(wget:*)` denies stay intact; and because two of the installers write a `PreToolUse` hook into settings themselves, it stops at each of those points and shows the diff — the generic guide's rule 4 (*a human reviews each hook before it lands*) applied to installers that would otherwise wire hooks in silently.

Install status is deliberately **not** recorded in this or any other doc: it is a per-box, manual job, so a written answer goes stale on the next clone. Run the report — that is the live answer.

## Related

- [../.specify/memory/constitution.md](../.specify/memory/constitution.md) — governance; supersedes this file and the generic guide alike.
- [RELEASING.md](RELEASING.md) — the release matrix and its contracts.
- [conventions/change-intent-records.md](conventions/change-intent-records.md) — when a change warrants a CIR; [conventions/csharp-style.md](conventions/csharp-style.md) — C# style.
- [../.claude/hooks/README.md](../.claude/hooks/README.md) — per-hook documentation.
- [study/README.md](study/README.md) — the `/study` records: what surprised a piece of work, classified by where the fix belongs, so the harness can be improved from evidence.
