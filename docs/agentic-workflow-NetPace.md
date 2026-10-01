# Agentic Workflow — the NetPace delta

Companion to [agentic-workflow.md](agentic-workflow.md), the stack-neutral generic guide. This file records **only where NetPace differs** from it or makes it concrete; anything not listed here follows the generic guide as written. NetPace-specific behaviour never edits the generic guide.

## Platform

- **Cross-platform, developed on Windows + WSL.** NetPace targets `win`/`linux`/`osx` (`x64`/`arm64`) and is developed on Windows with a WSL sandbox for the agent (see [wsl-claude-sandbox.md](wsl-claude-sandbox.md)).
- **AOT-trimmable.** Production code must stay trim/AOT-safe (reflection-heavy APIs like `Spectre.Console.Cli` were deliberately replaced). A code constraint, not a workflow one, but it limits what "implement" may reach for.

## CI

The generic "CI on PR" step **applies fully**:

| Workflow | Trigger | Role |
|---|---|---|
| `dotnet.yml` — Build and Test | pull_request → main | the generic **CI-on-PR** gate: build + test every PR |
| `shell-tests.yml` — Shell Tests | pull_request → main | runs every committed `*.tests.sh` matrix, one job per script |
| `traceability.yml` — Traceability | pull_request → main | the generic **scenario-traceability** gate: every `**Scenario:**` label on the branch's issue has a matching test marker |
| `codeql.yml` — CodeQL | push/PR/weekly | security analysis (see the [supply-chain CIR](change-intent-records/2026-07-13-dependency-supply-chain-hardening.md)) |
| `claude.yml` — Claude Code | `@claude` in an issue/PR comment by `FrankRay78` | **Review B** |
| `reviewissue.yml` — Review Issue | `review` label applied to an issue (labeller-gated), or manual dispatch | posts the issue gap analysis unattended — see below |
| `confirmissue.yml` — Confirm Issue | `confirm` label applied to an issue (labeller-gated), or manual dispatch | folds the answered review into the issue body unattended — see below |
| `publish-nuget.yml` | tag push | publish `NetPace.Core` to NuGet |
| `release-binaries.yml` | tag push | cross-platform binary release matrix |

**Deviations from the generic Appendix.** Review B is mention-triggered only (`/raise-pr` posts the mention); there is no automatic review of agent-authored PRs. There is no PR template and no `AGENTS.md` symlink.

**The whole pre-build issue gate runs in CI.** Both halves are label-triggered, so an issue raised away from the desk can be reviewed, answered and confirmed without a checkout. Labelling `review` runs [`.claude/commands/reviewissue.md`](../.claude/commands/reviewissue.md) and leaves the gap analysis as a comment; the author answers it inline in the GitHub UI; labelling `confirm` then runs [`.claude/commands/confirmissue.md`](../.claude/commands/confirmissue.md), which folds the answers into the issue body and deletes the comment. Each workflow applies its command as written, keeping the logic single-sourced. Both commands still run locally, unchanged.

The two workflows share one per-issue concurrency group, `issue-<n>`. Concurrency groups are repo-wide, so the shared *name* is what stops a review posting on top of a confirmation in flight.

**The labels are the state** — the generic *Context management* rule, made concrete. A green run always removes the label that triggered it. So:

- **`review` labelled** — a review is pending; if it stays labelled, the run did not complete.
- **`needs answers`, with a `<!-- speckit:review -->` comment** — a review is posted and awaiting answers.
- **`confirm` labelled** — a confirmation is pending; if it stays labelled, the run did not complete. On one of the command's hard-stops the issue is untouched and the answers are still there to fix and re-label; on a failed post-condition the decisions may already be on the body with a half-applied set of labels. The run summary is what tells the two apart.
- **`ready`, with a `## Confirmed decisions` section** — the gate is finished. `ready` takes precedence: an issue confirmed before these changes carries both markers and is finished, not waiting.

There is no failure comment on either path: GitHub's failed-run notification is the alert, and each workflow checks its own post-conditions. The confirm workflow checks four — decisions on the body, `ready` on, `needs answers` off, no surviving review comment — because three operations across the command's steps 6 and 7 are never fatal, and without that check a half-finished confirmation would go green. It names every missing half in one run, and puts the command's report in the run summary, which on a stop is the only place the outstanding gaps appear.

Labelling an issue that is `ready` posts nothing and just clears the label, on either path; labelling `review` on an issue that already has one does the same. To re-review a confirmed issue, remove `ready` first. Refining a review in place (step 6 of the review command) stays local. One gap on both paths: a label applied by any account other than the gated one creates no run at all, so the issue stays labelled but silent. Rationale and residuals: CIRs [`2026-09-07-automated-prespec-review`](change-intent-records/2026-09-07-automated-prespec-review.md), [`2026-09-11-confirmed-decisions-replace-the-review`](change-intent-records/2026-09-11-confirmed-decisions-replace-the-review.md) and [`2026-09-26-automated-prespec-confirmation`](change-intent-records/2026-09-26-automated-prespec-confirmation.md).

## Release pipeline

NetPace ships `NetPace.Core` as a **NuGet package** and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md). Touching `release-binaries.yml` (or other release-pipeline scope) without updating it is a documented no-no (CLAUDE.md rule). Release notes are GitHub-auto-generated from merged PRs; there is no `CHANGELOG.md`.

## Decision ledger: Change-Intent-Records **and** memory

Where the generic guide offers "Change-Intent-Records (or an equivalent decision ledger)", NetPace uses **both, for different jobs**:

- **Change-Intent-Records** — dated `YYYY-MM-DD-slug.md` files in [`docs/change-intent-records/`](change-intent-records/): the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the automated issue gate, supply-chain hardening). When to write one: [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`: agent-facing facts and corrections, one per file. "Prefer a gate to a memory entry" is live here: several memories survive only as the *rationale* for a gate that now enforces them (`feedback_trusting_a_test_run` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer as NetPace wires it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), are wired in `.claude/settings.json`, and are documented in [`.claude/hooks/README.md`](../.claude/hooks/README.md).

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when no test assembly is built or a `*.cs` under `src/` is newer than it | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks any `git commit` while a skip-family construct (incl. xUnit-v3 `SkipUnless=`/`SkipWhen=`) exists under `src/`; a `--check` mode exists, but no workflow runs it yet | PreToolUse(Bash) |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | **`/verify`'s formatting pass (step 1a) — `dotnet format style/whitespace ./src/NetPace.sln`, once per PR. Not a hook** (see below) | — |
| **Test-green gate** | **`/verify`'s suite gate (step 1b) — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |
| **Scenario traceability** | **`scripts/traceability-check.sh` — fails naming any `**Scenario: X**` label on the branch's issue with no matching `SCENARIO: X` marker in a committed test file. Not a hook:** the [`traceability.yml`](../.github/workflows/traceability.yml) `traceability` job runs it on every PR, and agents run it locally before raising one | pull_request → main |
| Fast/slow test categories | none: the suite is fully mocked and fast, so there is no split | — |

Every hook has an **announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`). `green-gate.sh` fails open. `no-skipped-tests.sh` fails closed once a call is classified as a `git commit`, since a skip ban that fails open is the silent non-coverage it exists to stop.

`traceability-check.sh` is not a hook and has **no override**: it fails closed on a missing tool, an unresolvable repository, an auth, network or rate-limit failure, a reply whose body it cannot read, an issue body with an unterminated code fence, and a marker scan that cannot complete — because a merge gate that passes without having run gives exactly the false comfort it exists to remove. The quiet passes are enumerated rather than incidental: GitHub answering "nothing at that number" is an answer rather than an outage, so a branch with no issue number, a number that is a pull request, a number that resolves to nothing, and an issue carrying no labels each pass with nothing to check — and each says which of those it is, so the four are distinguishable in a log. Its `traceability` job is a fixed-name context, but **it is not in the `Main CI/CD` ruleset yet** — adding it there is the post-merge step on issue #319, and until it is done a failure is reported on the PR without blocking the merge.

Each hook is a **script with a `.tests.sh` case matrix beside it** (generic *Modifying the harness itself*, rule 1). The exception is the PR pre-flight: an inline command in `settings.json`, so a red suite exits 1, not 2 — it is reported but does not block `gh pr create`. The binding gate is `/verify`'s suite run. **Every `*.tests.sh` in the repo runs in CI**: [`shell-tests.yml`](../.github/workflows/shell-tests.yml) discovers them with `git ls-files '*.tests.sh'` rather than naming them, one matrix entry per script with `fail-fast: false`, so a new matrix is gated the moment it is committed and every failure is reported against the script that produced it. Its fixed-name `shell-tests` job is the one stable context branch protection can require; **it is not in the `Main CI/CD` ruleset yet**, so today a red matrix is reported on the PR without blocking the merge.

**Two generic gates do not apply.** There is **no stack-guard** (no external service stack to orchestrate) and **no UI-automation denylist** (a console CLI has no browser UI to guard).

**Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — how a CLI covers the *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

### Formatting

Formatting runs **once per PR**, as `/verify`'s formatting pass (step 1a), never on commit — the generic *Formatting is not verification* section, made concrete:

```bash
dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
```

The explicit solution argument is **required**: `dotnet format` only looks in the *current directory*, and NetPace's solution lives under `src/`. Without it the command fails.

**Why not per-commit.** Measured when the solution had 84 `.cs` files (~9,900 LOC): **21.4s** for one staged file, **29.5s** for seven. The cost is MSBuild **workspace load**, not file count, so every commit would pay ~20–30s regardless. A release cycle's drift, by contrast, is a handful of import reorderings and a couple of hundred trailing spaces on blank lines — nothing a reviewer would catch. The guide's "tens of seconds" holds even at this size, so "our solution is small enough to absorb it" does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, matching `.editorconfig`'s `end_of_line = lf` and the LF the index stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` rewrites every file it touches — no committed diff, since commit normalises back, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* the attribute needs a one-time refresh (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/build`

Follows the generic *build stage*. NetPace's specifics:

- **Branch:** `feature/<N>-<short-slug>`, cut from `origin/main`. `/raise-pr` and `/study` read the issue number from this pattern.
- **Commits:** `Refs #<N>: …` in the imperative mood (constitution, *Git Workflow*). The failing tests are committed with the implementation that turns them green, never on their own.
- **Suite:** as in *The gates, concretely*.
- **Docs it must update** (`CLAUDE.md`'s paired rules): `///` XML docs on any new or changed public `NetPace.Core` API; the README.md `--help` snapshot and USER_GUIDE.md for a changed CLI option; `docs/RELEASING.md` for a release-pipeline change; a Change-Intent Record where the change is non-obvious.

## `/verify`

Follows the generic *verify gate*. NetPace's specifics:

- **Steps 1a/1b:** the formatting and test-green rows in *The gates, concretely*.
- **Review A (step 2):** two waves of the applicable reviewers. Wave 1: the five report-only `pr-review-toolkit` reviewers and `/review-slop`, together. Wave 2: `pr-review-toolkit:code-simplifier`, which edits files, alone.

## `/study`

Follows the generic *study pass*. Records go to `docs/study/<N>.md`; the four levels are defined in [study/README.md](study/README.md). Each run commits `Refs #<N>: record study findings` on the feature branch.

`/study-review` reads the records back periodically. It scores recurring findings and offers numbered fixes to implement or raise as issues. It keeps no state and never edits a record.

## Permissions

The mechanism is in the generic *Permissions and unattended runs*. NetPace's rule changes:

- `Bash(rm:*)` and `Bash(rmdir:*)` came off `ask` ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)).
- `Bash(git push:*)` moved to `allow`, so `/raise-pr` pushes without stopping; `Bash(chmod:*)` moved from `deny` to `ask` ([CIR](change-intent-records/2026-09-04-push-allow-chmod-ask.md)).
- The six `Read(…)` deny rules were removed: their glob scope made every recursive read escalate to an approval no mode auto-grants ([CIR](change-intent-records/2026-09-04-read-deny-rules-removed.md)).

`chmod` is the only `ask` rule, so it is the one call a chained stage could lose silently — and the executable-bit forms no longer do. [`no-chmod.sh`](../.claude/hooks/no-chmod.sh) refuses them first with a message naming `bash script.sh`, so the agent is redirected rather than stalled or quietly degraded ([CIR](change-intent-records/2026-09-27-refuse-executable-bit-chmod.md)). The `ask` rule stays in place as the fail-open backstop for the forms the hook declines to decide.

## Chain

Why `scripts/chain.sh` opens the PR without a pause: [CIR](change-intent-records/2026-09-14-chain-raises-pr-unattended.md).

The chain runner (`scripts/chain-next.sh`, generic *The chain runner*) runs on the build VPS as a systemd user timer, with lingering already enabled for the build user. Its dedicated clone is `~/Repos/NetPace-runner`, cloned from `https://github.com/FrankRay78/NetPace.git`, and its logs are in `~/.local/state/netpace-chain/logs/`. The shipped unit files work there unedited. `ready` is the only opt-in, so confirming an issue queues it. There is no separate queue label, and the runner never merges. It supersedes the dispatcher proposed in #266.

## Token / context tooling

Two of the three tools are wired into config. `rtk` has `Bash(rtk …)` allow-entries in `.claude/settings.json` and a prefix-strip in `green-gate.sh`'s `strip_cmd_prefixes()`, which assumes rtk may be in play. `context-mode` has `mcp__plugin_context-mode_context-mode__*` allow-entries and an `enabledPlugins` entry. `read-once` appears in the guides but in no config.

Nothing else checks the tools are installed. [`scripts/plugin-report.sh`](../scripts/plugin-report.sh) does: a manually-run report in four sections — `TOOLING` (each expected tool, `pr-review-toolkit` included: declared / installed / enabled / reachable), `CONFIG` (unresolvable hook and statusLine paths, dangling or duplicated allow-entries), `HOOKS` (what is registered and its per-invocation cost), and `PERFORMANCE` (live savings counters, where a tool exposes them).

```bash
bash scripts/plugin-report.sh
```

It installs nothing and changes no repo file, but it is not inert. Reading context-mode's counters means asking context-mode through an MCP tool, so it starts a headless `claude -p` — costing money and seconds, and needing the network and a logged-in CLI. `context-mode doctor` also checks the npm registry, and context-mode's CLI creates its empty storage directories if absent.

It is not a gate: no `--check` mode, no exit-code contract, and no hook, CI job or `/verify` step runs it. It is meant to be run on two boxes and diffed, so `TOOLING`, `CONFIG` and `HOOKS` carry no timestamps, absolute paths or raw millisecond figures. `PERFORMANCE` is exempt (its counters move every session), so cross-box diffs use the other three. A probe that cannot reach a verdict reports `unknown`, per the generic guide.

[`/install-harness-tooling`](../.claude/commands/install-harness-tooling.md) is the other half: it installs what the report finds missing. It reads each upstream `install.sh` first and **prints** the command for a human to run rather than running it, keeping the `Bash(curl:*)` / `Bash(wget:*)` denies intact. Two installers write a `PreToolUse` hook into settings themselves, so it stops at each and shows the diff — the generic rule 4 (*a human reviews each hook before it lands*) applied to installers that would otherwise wire hooks in silently.

Install status is deliberately **not** recorded in any doc: it is per-box and manual, so a written answer goes stale on the next clone. Run the report for the live answer.

## Related

- [constitution.md](constitution.md) — governance; supersedes this file and the generic guide alike.
- [RELEASING.md](RELEASING.md) — the release matrix and its contracts.
- [conventions/change-intent-records.md](conventions/change-intent-records.md) — when a change warrants a CIR; [conventions/csharp-style.md](conventions/csharp-style.md) — C# style.
- [../.claude/hooks/README.md](../.claude/hooks/README.md) — per-hook documentation.
- [study/README.md](study/README.md) — the `/study` records: what surprised a piece of work, classified by where the fix belongs, so the harness can be improved from evidence.
