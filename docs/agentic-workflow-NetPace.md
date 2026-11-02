# Agentic Workflow — the NetPace delta

Companion to the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin's [workflow guide](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/workflow.md), the stack-neutral generic guide to the workflow its commands implement. This file records **only where NetPace differs** from it or makes it concrete; anything not listed here follows the generic guide as written, and the rules the commands apply here are in [`CONVENTIONS.md`](../CONVENTIONS.md) and [`REVIEW.md`](../REVIEW.md), not restated below. NetPace-specific behaviour never edits the generic guide: a change to it, or to a command, is an issue or pull request on gaffer.

## Platform

- **Cross-platform, developed on Windows + WSL.** NetPace targets `win`/`linux`/`osx` (`x64`/`arm64`) and is developed on Windows with a WSL sandbox for the agent (see [wsl-claude-sandbox.md](wsl-claude-sandbox.md)).
- **AOT-trimmable.** Production code must stay trim/AOT-safe (reflection-heavy APIs like `Spectre.Console.Cli` were deliberately replaced). A code constraint, not a workflow one, but it limits what "implement" may reach for.

## CI

The generic "CI on PR" step **applies fully**:

| Workflow | Trigger | Role |
|---|---|---|
| `dotnet.yml` — Build and Test | pull_request → main | the generic **CI-on-PR** gate: build + test every PR, and the format check `/gaffer:verify` formats against |
| `shell-tests.yml` — Shell Tests | pull_request → main | runs every committed `*.tests.sh` matrix, one job per script |
| `traceability.yml` — Traceability | pull_request → main | the generic **scenario-traceability** gate: every `**Scenario:**` label on the branch's issue has a matching test marker |
| `link-check.yml` — Link Check | pull_request → main | the **link-check** gate: every markdown link, and every bare citation of a change-intent record, resolves to a file in the repository |
| `pr-issue-link.yml` — PR Issue Link | pull_request (incl. `edited`) → main | the **issue-link** gate: merging the PR must close an open issue in this repository |
| `clear-workflow-labels.yml` — Clear Workflow Labels | pull_request `closed` → main | takes the issue-chain labels off every issue in this repository the merge closed |
| `codeql.yml` — CodeQL | push/PR/weekly | security analysis (see the [supply-chain CIR](change-intent-records/2026-07-13-dependency-supply-chain-hardening.md)) |
| `claude.yml` — Claude Code | `@claude` in an issue/PR comment by `FrankRay78` | on-demand agent help on an issue or PR |
| `review-pr.yml` — Review PR | pull_request `opened`/`ready_for_review` by `FrankRay78`, or manual dispatch | **Review B**: runs gaffer's `/gaffer:review-pr` and posts it unattended, then records what the round cost on the PR |
| `review-followup.yml` — Review Followup | `review-followup` label applied to a PR (labeller-gated) | runs gaffer's `/gaffer:review-followup` unattended for the next round, records what it cost, and removes the label — see below |
| `reviewissue.yml` — Review Issue | `review` or `refine` label applied to an issue (labeller-gated), or manual dispatch | posts the issue gap analysis, or re-frames the questions that asked for help, unattended — see below |
| `confirmissue.yml` — Confirm Issue | `confirm` label applied to an issue (labeller-gated), or manual dispatch | folds the answered review into the issue body unattended — see below |
| `publish-nuget.yml` | tag push | publish `NetPace.Core` to NuGet |
| `release-binaries.yml` | tag push | cross-platform binary release matrix |
| `publish-tool.yml` | semver tag push | publish the `NetPace` .NET tool package to NuGet |
| `homebrew-publish.yml` — Publish Homebrew Tap | `release-binaries.yml` completing, or manual dispatch | render the `netpace` formula from the release, install it on three runners, then push it to the Homebrew tap |

**Deviations from the generic guide.** Review B runs once, when the PR opens; a later push is not re-reviewed automatically, and a follow-up round is started deliberately, by adding the `review-followup` label to the PR. There is no PR template and no `AGENTS.md` symlink.

**Where the commands come from.** The four Claude workflows follow the shape gaffer's [`docs/unattended.md`](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/unattended.md) → *In CI* describes — gated on the account that opened the PR or applied the label, `main`'s `REVIEW.md` and `CONVENTIONS.md` put in place before the agent starts, gaffer cloned from its `main` in a step of its own and passed as `--plugin-dir`, the post-condition asserted afterwards, one concurrency group per PR or issue. Two things are NetPace's. The clone is made read-only before the agent starts, because `--add-dir` makes it a working directory the agent's `Write` reaches and the plugin's hook runs from it on every Bash call. And the one way a pull request can still choose which gaffer reviews it is the workflow file itself: `review-pr` and `review-followup` run on `pull_request`, for which GitHub runs the workflow file from the pull request. So a change to a command takes effect on its merge in gaffer, not here; a change to `REVIEW.md` or `CONVENTIONS.md` takes effect on merge here. Rationale: CIR [`2026-11-01-netpace-runs-gaffers-commands`](change-intent-records/2026-11-01-netpace-runs-gaffers-commands.md).

**Every review round runs in CI, and says what it cost.** The first review runs on PR open; labelling `review-followup` runs the next round and the label comes off when the run ends, so adding it again starts the one after. The cap is unchanged at three rounds, and a label added past it, or on a PR with no first review yet, is a green run that posts nothing. Each round that posts adds a row — `PR Review`, then `Review 1`, `Review 2` — to a table at the foot of the PR description, with a `Total` row under it, so a merged PR carries the cost of reviewing it. The rows come from [`scripts/pr-cost-footer.sh`](../scripts/pr-cost-footer.sh); a round run by hand from `/gaffer:review-pr` or `/gaffer:review-followup` locally still works and adds no row. Rationale: the [cost-footer CIR](change-intent-records/2026-10-28-review-rounds-record-what-they-cost.md).

**The whole pre-build issue gate runs in CI.** Every step is label-triggered, so an issue raised away from the desk can be reviewed, answered and confirmed without a checkout. Labelling `review` runs gaffer's [`reviewissue`](https://github.com/FrankRay78/gaffer-claude/blob/main/commands/reviewissue.md) command and leaves the gap analysis as a comment; the author answers it inline in the GitHub UI; where an answer asks for help rather than deciding, labelling `refine` runs the refine step of the same command and re-frames just those questions in place; labelling `confirm` then runs gaffer's [`confirmissue`](https://github.com/FrankRay78/gaffer-claude/blob/main/commands/confirmissue.md), which folds the answers into the issue body and deletes the comment. Both commands still run locally.

**One label per issue state.** Each of the three does exactly one job, and which one fits is decided by where the issue stands:

| Issue state | Label that fits | What it does |
|---|---|---|
| No review yet | `review` | Posts the gap analysis |
| Review posted, answers not yet written | none | Answer the questions first |
| Review posted, at least one answer asks for help | `refine` | Re-frames only those questions, in place |
| Review posted, answers concrete | `confirm` | Folds the answers into the body and marks it `ready` |
| `ready` | none | Nothing left to do |

A label applied in any other state is a **wrong-state trigger**: it changes nothing on the issue, removes the label it was triggered by so the right one can be applied straight away, and finishes green without saying which label would have fitted. Two states are *not* wrong-state triggers, and both go red with the label left on: `ready` on an issue that still carries a review comment, which is a half-confirmed issue needing manual repair, and `confirm` on a review whose answers are still unanswered or hedging, which the confirm command stops on by design. The three paths share one per-issue concurrency group, `issue-<n>`.

**The labels are the state** — the generic *Context management* rule, made concrete. A green run always removes the label that triggered it. So:

- **`review` labelled** — a review is pending; if it stays labelled, the run did not complete.
- **`needs answers`, with a `<!-- speckit:review -->` comment** — a review is posted and awaiting answers.
- **`refine` labelled** — a refine is pending; if it stays labelled, the run did not complete. `needs answers` stays on throughout: a re-framed question is still a question.
- **`confirm` labelled** — a confirmation is pending; if it stays labelled, the run did not complete. On one of the command's hard-stops the issue is untouched and the answers are still there to fix and re-label; on a failed post-condition the decisions may already be on the body with a half-applied set of labels. The run summary is what tells the two apart.
- **`ready`, with a `## Confirmed decisions` section** — the gate is finished. `ready` takes precedence: an issue confirmed before these changes carries both markers and is finished, not waiting.

**A merge ends the chain.** When a pull request merges, `clear-workflow-labels.yml` takes every one of these labels off each issue it closed, so a label view is not answered by issues the merge already finished. An issue closed any other way keeps its labels. Rationale: CIR [`2026-10-18-merge-clears-issue-workflow-labels`](change-intent-records/2026-10-18-merge-clears-issue-workflow-labels.md).

There is no failure comment on any path: GitHub's failed-run notification is the alert, and each workflow asserts its own post-conditions after the agent — the confirm workflow checks five (decisions on the body, `ready` on, `needs answers` off, no surviving review comment, the agent's report written) and names every missing one in a single run, with the command's report in the run summary. To re-review a confirmed issue, remove `ready` first and delete any review comment still on the thread. One gap on all three paths: a label applied by any account other than the gated one creates no run at all, so the issue stays labelled but silent. Rationale and residuals: CIRs [`2026-09-07-automated-prespec-review`](change-intent-records/2026-09-07-automated-prespec-review.md), [`2026-09-11-confirmed-decisions-replace-the-review`](change-intent-records/2026-09-11-confirmed-decisions-replace-the-review.md), [`2026-09-26-automated-prespec-confirmation`](change-intent-records/2026-09-26-automated-prespec-confirmation.md).

## Release pipeline

NetPace ships `NetPace.Core` as a **NuGet package** and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md). Touching `release-binaries.yml` (or other release-pipeline scope) without updating it is a documented no-no (`CONVENTIONS.md` → *Documentation*). Release notes are GitHub-auto-generated from merged PRs; there is no `CHANGELOG.md`.

## Decision ledger: Change-Intent-Records **and** memory

The generic guide's *Documentation hierarchy* leaves the detailed-reference tier to the repository; NetPace fills it with **two ledgers, for different jobs**:

- **Change-Intent-Records** — dated `YYYY-MM-DD-slug.md` files in [`docs/change-intent-records/`](change-intent-records/): the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the automated issue gate, supply-chain hardening). When to write one: [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`: agent-facing facts and corrections, one per file. "Prefer a gate to a memory entry" is live here: several memories survive only as the *rationale* for a gate that now enforces them (`feedback_trusting_a_test_run` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer as NetPace wires it. `CONVENTIONS.md` → *Hooks, scripts and plugins* names what runs and why; this table maps each generic gate onto it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), are wired in `.claude/settings.json`, and are documented in [`.claude/hooks/README.md`](../.claude/hooks/README.md); each script's header holds its own failure modes and what it deliberately does not check.

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when no test assembly is built or a `*.cs` under `src/` is newer than it | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks any `git commit` while a skip-family construct exists under `src/`; a `--check` mode exists, but no workflow runs it yet | PreToolUse(Bash) |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | `/gaffer:verify`'s formatting passes, `dotnet format ./src/NetPace.sln` (`CONVENTIONS.md` → *Build, test and format*). Not a hook (see below) | — |
| **Formatting, gated** (no generic counterpart) | `dotnet format ./src/NetPace.sln --verify-no-changes` in [`dotnet.yml`](../.github/workflows/dotnet.yml). Not a hook, and no script: the step is inline in the workflow | pull_request → main |
| **Test-green gate** | `/gaffer:verify`'s suite gate — a real `dotnet build ./src && dotnet test ./src`. Not a hook | — |
| **Scenario traceability** | [`scripts/traceability-check.sh`](../scripts/traceability-check.sh) (`CONVENTIONS.md` → *Acceptance criteria*). Not a hook: the [`traceability.yml`](../.github/workflows/traceability.yml) `traceability` job runs it on every PR, and agents run it locally before raising one | pull_request → main |
| **PR links an issue** (no generic counterpart) | the `issue-link` job in [`pr-issue-link.yml`](../.github/workflows/pr-issue-link.yml) (`CONVENTIONS.md` → *Pull requests*). Not a hook, and no script: the check is inline in the workflow | pull_request → main |
| **Repository references resolve** (no generic counterpart) | [`scripts/link-check.sh`](../scripts/link-check.sh) (`CLAUDE.md` → *Working with Claude Code*). Not a hook: the [`link-check.yml`](../.github/workflows/link-check.yml) `link-check` job runs it on every PR | pull_request → main |
| Fast/slow test categories | none: the suite is fully mocked and fast, so there is no split | — |

Every hook has an **announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`). `green-gate.sh` fails open. `no-skipped-tests.sh` fails closed once a call is classified as a `git commit`, since a skip ban that fails open is the silent non-coverage it exists to stop. The three scripts that run as CI jobs — `traceability-check.sh`, `link-check.sh` and the inline `issue-link` check — have **no override** and fail closed on anything that stops them reaching an answer, because a merge gate that passes without having run gives exactly the false comfort it exists to remove; their headers enumerate the quiet passes.

**Which gates block the merge.** Of the fixed-name contexts, only `traceability` is a required check in the `Main CI/CD` ruleset today. `link-check`, `issue-link` and `shell-tests` are reported on the PR without blocking it; registering each, and deleting the surviving repository-admin bypass actor so the rule binds everyone including Frank, are changes to make by hand in the ruleset (GitHub → Settings → Rules → `Main CI/CD`; `gh api repos/FrankRay78/NetPace/rulesets/4185732` reads the result back), after the workflow is on `main`, because requiring a context whose workflow is not yet on `main` blocks every other open pull request.

Each hook is a **script with a `.tests.sh` case matrix beside it** (generic *Modifying a harness*, rule 1). The exception is the PR pre-flight: an inline command in `settings.json`, so a red suite exits 1, not 2 — it is reported but does not block `gh pr create`. The binding gate is `/gaffer:verify`'s suite run. **Every `*.tests.sh` in the repo runs in CI**: [`shell-tests.yml`](../.github/workflows/shell-tests.yml) discovers them with `git ls-files '*.tests.sh'` rather than naming them, one matrix entry per script with `fail-fast: false`, so a new matrix is gated the moment it is committed.

**Two generic gates do not apply.** There is **no stack-guard** (no external service stack to orchestrate) and **no UI-automation denylist** (a console CLI has no browser UI to guard).

**Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — how a CLI covers the *verify* duty for rendered output (`CONVENTIONS.md` → *Testing*). Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

### Formatting

Formatting runs inside `/gaffer:verify`, never on commit — the generic *Formatting is not verification* section, made concrete. The command, and why it is the bare one CI runs, is `CONVENTIONS.md` → *Build, test and format*. What that section does not hold:

**How often.** Once up front, and once more if the review applied edits, before the suite re-run. That is at most two invocations per pull request, not one per commit. Formatting only up front would leave every review fix unformatted; formatting *after* the suite re-run would commit bytes the suite never saw.

**Why not per-commit.** Measured when the solution had 84 `.cs` files (~9,900 LOC): **21.4s** for one staged file, **29.5s** for seven. The cost is MSBuild **workspace load**, not file count, so every commit would pay ~20–30s regardless. A release cycle's drift, by contrast, is a handful of import reorderings and a couple of hundred trailing spaces on blank lines — nothing a reviewer would catch.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, matching `.editorconfig`'s `end_of_line = lf` and the LF the index stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` rewrites every file it touches — no committed diff, since commit normalises back, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* the attribute needs a one-time refresh (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## Permissions

The mechanism is in the generic *Permissions and unattended runs*. NetPace's rule changes:

- `Bash(rm:*)` and `Bash(rmdir:*)` came off `ask` ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)).
- `Bash(git push:*)` moved to `allow`, so `/gaffer:raise-pr` pushes without stopping; `Bash(chmod:*)` moved from `deny` to `ask` ([CIR](change-intent-records/2026-09-04-push-allow-chmod-ask.md)).
- The six `Read(…)` deny rules were removed: their glob scope made every recursive read escalate to an approval no mode auto-grants ([CIR](change-intent-records/2026-09-04-read-deny-rules-removed.md)).

`chmod` is the only `ask` rule, so it is the one call a chained stage could lose silently — and the executable-bit forms no longer do. Gaffer's [`no-chmod` hook](https://github.com/FrankRay78/gaffer-claude/blob/main/hooks/no-chmod.sh), which runs here because the plugin is enabled, refuses them first with a message naming `bash script.sh`, so the agent is redirected rather than stalled or quietly degraded. The `ask` rule stays in place as the fail-open backstop for the forms the hook declines to decide.

## Chain

The chain is gaffer's: [`chain/chain.sh`](https://github.com/FrankRay78/gaffer-claude/blob/main/chain/chain.sh) runs the three stages for one issue and [`chain/chain-next.sh`](https://github.com/FrankRay78/gaffer-claude/blob/main/chain/chain-next.sh) picks the next ready issue from a timer, both documented in gaffer's [`docs/chain.md`](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/chain.md) — selection, the reset, parking, the verdict reading, the usage-limit stop and the permissions opt-in. NetPace schedules it: a systemd user timer on the build VPS fires [`scripts/systemd/netpace-chain-next.service`](../scripts/systemd/netpace-chain-next.service) every 15 minutes, which fast-forwards the runner's clone of gaffer at `~/Repos/gaffer-runner` to gaffer's `main` (under the runner's own lock, so a run in progress is never updated beneath) and runs its `chain-next.sh` against [`.gaffer/chain.json`](../.gaffer/chain.json) in the runner's dedicated clone at `~/Repos/NetPace-runner` (a full clone marked `chain-next.dedicated`). That file is NetPace's whole configuration of the chain: the clone, the writers whose issues the runner may build (the maintainer and the review bot — anyone else's issue is built by hand), each stage's model and effort, and `skip_permissions: true`, the opt-in the headless stages need under NetPace's permission rules. The runner reads the file from the clone as the last building firing left it (that issue's feature or `attempt/` branch; an empty firing resets nothing), so a merged change reaches selection only after a later building firing has reset the clone. Of gaffer's conditions for the opt-in, NetPace meets the dedicated clone and the machine set aside for the runner; the `gh` and `claude` sign-ins on that machine are the maintainer's own, not credentials scoped to what the chain pushes and comments on, which is the residual exposure. The unit's `PATH` names where `claude`, `dotnet`, `gh`, `jq`, `git`, `flock` and `timeout` live, lingering is enabled for the build user, and logs and the lock sit under `~/.local/state/gaffer-chain/NetPace-runner/`. The unit files in [`scripts/systemd/`](../scripts/systemd/) are the setup, with the one-time steps in their headers. Why the chain opens the PR without a pause: gaffer's workflow guide, *Running the chain*; why it no longer studies: [CIR](change-intent-records/2026-10-09-study-removed.md).

**Operating it:**

| To | Run |
|---|---|
| Start (and on every boot) | `systemctl --user enable --now netpace-chain-next.timer` |
| Pause (a run in progress finishes) | `systemctl --user stop netpace-chain-next.timer` |
| Stop a run in progress too (the issue is not parked) | `systemctl --user stop netpace-chain-next.service` |
| Fire once now | `systemctl --user start --no-block netpace-chain-next.service` |
| See status and the next firing | `systemctl --user status netpace-chain-next.service` and `systemctl --user list-timers netpace-chain-next.timer` |
| Read the runner's own output | `journalctl --user -u netpace-chain-next.service` |
| Read one run's full chain output | `~/.local/state/gaffer-chain/NetPace-runner/logs/<N>-<timestamp>.log` |
| Retry a parked issue, or finish a parked attempt by hand | gaffer's `docs/chain.md`, *Setting it up* |

## Token / context tooling

Three token/context tools are in use: `rtk`, `context-mode` and `read-once` (described, with their install commands, in [wsl-claude-sandbox.md](wsl-claude-sandbox.md), step 7). Two are wired into config. `rtk` has `Bash(rtk …)` allow-entries in `.claude/settings.json` and a prefix-strip in `green-gate.sh`'s `strip_cmd_prefixes()`, which assumes rtk may be in play. `context-mode` has `mcp__plugin_context-mode_context-mode__*` allow-entries and an `enabledPlugins` entry. `read-once` appears in that guide but in no config. Nothing checks that any of them is installed: config can declare, allow and enable a tool that is missing from the machine, and a missing tool fails silently — the session just loses the savings.

Nothing else checks the tools are installed. [`scripts/plugin-report.sh`](../scripts/plugin-report.sh) does: a manually-run report in four sections — `TOOLING` (each expected tool, `pr-review-toolkit` included: declared / installed / enabled / reachable), `CONFIG` (unresolvable hook and statusLine paths, dangling or duplicated allow-entries), `HOOKS` (what is registered and its per-invocation cost), and `PERFORMANCE` (live savings counters, where a tool exposes them).

```bash
bash scripts/plugin-report.sh
```

It installs nothing and changes no repo file, but it is not inert. Reading context-mode's counters means asking context-mode through an MCP tool, so it starts a headless `claude -p` — costing money and seconds, and needing the network and a logged-in CLI. `context-mode doctor` also checks the npm registry, and context-mode's CLI creates its empty storage directories if absent.

It is not a gate: no `--check` mode, no exit-code contract, and no hook, CI job or `/gaffer:verify` step runs it. It is meant to be run on two boxes and diffed, so `TOOLING`, `CONFIG` and `HOOKS` carry no timestamps, absolute paths or raw millisecond figures. `PERFORMANCE` is exempt (its counters move every session), so cross-box diffs use the other three. A probe that cannot reach a verdict reports `unknown`, never a confident `no`. `TOOLING` also reports `gaffer`, which supplies the commands: a box that gets it only through this repository's settings reports it declared and enabled but not installed, which is the honest answer.

[`/install-harness-tooling`](../.claude/commands/install-harness-tooling.md) is the other half: it installs what the report finds missing. It reads each upstream `install.sh` first and **prints** the command for a human to run rather than running it, keeping the `Bash(curl:*)` / `Bash(wget:*)` denies intact. Two installers write a `PreToolUse` hook into settings themselves, so it stops at each and shows the diff — the generic rule 4 (*a human reviews each hook before it lands*) applied to installers that would otherwise wire hooks in silently.

Install status is deliberately **not** recorded in any doc: it is per-box and manual, so a written answer goes stale on the next clone. Run the report for the live answer.

## Related

- [CONVENTIONS.md](../CONVENTIONS.md) — the rules of the codebase and what the harness commands apply here; [REVIEW.md](../REVIEW.md) — what the pull request review checks.
- [RELEASING.md](RELEASING.md) — the release matrix and its contracts.
- [conventions/change-intent-records.md](conventions/change-intent-records.md) — when a change warrants a CIR; [conventions/csharp-style.md](conventions/csharp-style.md) — C# style.
- [../.claude/hooks/README.md](../.claude/hooks/README.md) — per-hook documentation.
