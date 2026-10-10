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
| `pr-issue-link.yml` — PR Issue Link | pull_request (incl. `edited`) → main | the **issue-link** gate: merging the PR must close an open issue in this repository |
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
| **Formatting** | **`/verify`'s formatting passes — `dotnet format ./src/NetPace.sln`, the bare command CI checks: once up front (step 1a) and again if the review applied edits (step 3). Not a hook** (see below) | — |
| **Formatting, gated** (no generic counterpart) | **`dotnet format ./src/NetPace.sln --verify-no-changes` in [`dotnet.yml`](../.github/workflows/dotnet.yml) — the check `/verify` formats against. Not a hook, and no script:** the step is inline in the workflow | pull_request → main |
| **Test-green gate** | **`/verify`'s suite gate (step 1b) — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |
| **Scenario traceability** | **`scripts/traceability-check.sh` — fails naming any `**Scenario: X**` label on the branch's issue with no matching `SCENARIO: X` marker in a committed test file. Not a hook:** the [`traceability.yml`](../.github/workflows/traceability.yml) `traceability` job runs it on every PR, and agents run it locally before raising one | pull_request → main |
| **PR links an issue** (no generic counterpart) | **the `issue-link` job in [`pr-issue-link.yml`](../.github/workflows/pr-issue-link.yml) — asks GitHub which issues merging the PR closes and fails unless one is open and in this repository. Not a hook, and no script:** the check is inline in the workflow | pull_request → main |
| Fast/slow test categories | none: the suite is fully mocked and fast, so there is no split | — |

Every hook has an **announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`). `green-gate.sh` fails open. `no-skipped-tests.sh` fails closed once a call is classified as a `git commit`, since a skip ban that fails open is the silent non-coverage it exists to stop.

`traceability-check.sh` is not a hook and has **no override**: it fails closed on a missing tool, an unresolvable repository, an auth, network or rate-limit failure, a reply whose body it cannot read, an issue body with an unterminated code fence, and a marker scan that cannot complete — because a merge gate that passes without having run gives exactly the false comfort it exists to remove. The quiet passes are enumerated rather than incidental: GitHub answering "nothing at that number" is an answer rather than an outage, so a branch with no issue number, a number that is a pull request, a number that resolves to nothing, and an issue carrying no labels each pass with nothing to check — and each says which of those it is, so the four are distinguishable in a log. Its `traceability` job is a fixed-name context and a required check in the `Main CI/CD` ruleset, so a failure blocks the merge.

The `issue-link` job has **no override** and no parser: it reads `closingIssuesReferences`, GitHub's own answer to "what will merging this close?", so a closing keyword in the body and an issue attached through the Development sidebar both count, and a bare `#N` does not. It fails if GitHub cannot be reached or the reply cannot be read. **`dependabot[bot]` is the only exempt author**; there is no label exemption. It is a fixed-name context, but **it is not in the `Main CI/CD` ruleset yet** — registering it, and deleting the surviving repository-admin bypass actor so the rule binds everyone including Frank, are two changes to make by hand in the ruleset (GitHub → Settings → Rules → `Main CI/CD`; `gh api repos/FrankRay78/NetPace/rulesets/4185732` reads the result back). Until both are done a failure is reported on the PR without blocking the merge, and a direct push to `main` by an admin still succeeds. Rationale: CIR [`2026-10-08-every-pr-closes-an-issue`](change-intent-records/2026-10-08-every-pr-closes-an-issue.md).

Each hook is a **script with a `.tests.sh` case matrix beside it** (generic *Modifying the harness itself*, rule 1). The exception is the PR pre-flight: an inline command in `settings.json`, so a red suite exits 1, not 2 — it is reported but does not block `gh pr create`. The binding gate is `/verify`'s suite run. **Every `*.tests.sh` in the repo runs in CI**: [`shell-tests.yml`](../.github/workflows/shell-tests.yml) discovers them with `git ls-files '*.tests.sh'` rather than naming them, one matrix entry per script with `fail-fast: false`, so a new matrix is gated the moment it is committed and every failure is reported against the script that produced it. Its fixed-name `shell-tests` job is the one stable context branch protection can require; **it is not in the `Main CI/CD` ruleset yet**, so today a red matrix is reported on the PR without blocking the merge.

**Two generic gates do not apply.** There is **no stack-guard** (no external service stack to orchestrate) and **no UI-automation denylist** (a console CLI has no browser UI to guard).

**Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — how a CLI covers the *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

### Formatting

Formatting runs inside `/verify`, never on commit — the generic *Formatting is not verification* section, made concrete:

```bash
dotnet format ./src/NetPace.sln
```

**This is the bare command, and that matters: it is exactly what CI runs** as `dotnet format ./src/NetPace.sln --verify-no-changes`. It was previously the narrower `dotnet format style … && dotnet format whitespace …`, which can leave a tree CI's check then rejects — a branch reported verified that fails the format check on its own pull request. The bare command also applies analyzer fixes, so read what it changed rather than assuming it only moved whitespace.

The explicit solution argument is **required**: `dotnet format` only looks in the *current directory*, and NetPace's solution lives under `src/`. Without it the command fails.

**How often.** Once up front (step 1a), and once more if the review applied edits, before the suite re-run (step 3). That is at most two invocations per pull request, not one per commit. Formatting only up front would leave every review fix unformatted, since step 1a runs before any fix exists; formatting *after* the suite re-run would commit bytes the suite never saw.

**Why not per-commit.** Measured when the solution had 84 `.cs` files (~9,900 LOC): **21.4s** for one staged file, **29.5s** for seven. The cost is MSBuild **workspace load**, not file count, so every commit would pay ~20–30s regardless. A release cycle's drift, by contrast, is a handful of import reorderings and a couple of hundred trailing spaces on blank lines — nothing a reviewer would catch. The guide's "tens of seconds" holds even at this size, so "our solution is small enough to absorb it" does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, matching `.editorconfig`'s `end_of_line = lf` and the LF the index stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` rewrites every file it touches — no committed diff, since commit normalises back, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* the attribute needs a one-time refresh (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/build`

Follows the generic *build stage*. NetPace's specifics:

- **Branch:** `feature/<N>-<short-slug>`, cut from `origin/main`. `/raise-pr`, `/verify`, `scripts/traceability-check.sh` and `scripts/chain-next.sh` all read the issue number from this pattern.
- **Commits:** `Refs #<N>: …` in the imperative mood (constitution, *Git Workflow*). The failing tests are committed with the implementation that turns them green, never on their own.
- **Suite:** as in *The gates, concretely*.
- **Docs it must update** (`CLAUDE.md`'s paired rules): `///` XML docs on any new or changed public `NetPace.Core` API; the README.md `--help` snapshot and USER_GUIDE.md for a changed CLI option; `docs/RELEASING.md` for a release-pipeline change; a Change-Intent Record where the change is non-obvious.

## `/verify`

Follows the generic *verify gate*. NetPace's specifics:

- **Steps 1a/1b:** the formatting and test-green rows in *The gates, concretely*.
- **Review A (steps 2–3):** one round over the whole branch diff, in two waves of the applicable reviewers. Wave 1: the five report-only `pr-review-toolkit` reviewers and `/review-slop`, together. Wave 2: `pr-review-toolkit:code-simplifier`, which edits files, alone. Confirmed Blockers and Importants in the branch's own code are fixed with the smallest change that removes each, and committed on a green re-run. Nothing inside `/verify` reviews that commit; Review B reads it on the pull request ([CIR](change-intent-records/2026-10-10-verify-reviews-once.md)).

## `/audit-codebase`

Follows the generic *codebase audit* periodic task. NetPace's specifics:

- **Argument:** a tag or commit that starts the "what changed" window, normally the last release tag. With none, the command asks and stops.
- **Lenses:** constitutional conformance, test-suite integrity, docs against code, harness consistency, and dead code and duplication.
- **Evidence gathered first:** the build and suite with coverage (`dotnet-coverage`), `dotnet list package --deprecated` and `--outdated`, `nuget-license`, Roslynator's unused-symbol search and `jscpd`. The four that are not part of the SDK run one-shot at a pinned version (`dotnet tool exec`, `npx`), so the command adds no tool manifest. A tool that cannot run is named in the report rather than stopping the audit.
- **Output:** one issue labelled `housekeeping`, never `ready`, `review` or `confirm`, so neither the chain runner nor the issue-review workflows pick it up.
- **Not covered:** security and mutation testing ([CIR](change-intent-records/2026-10-09-codebase-audit-command.md)).

## Permissions

The mechanism is in the generic *Permissions and unattended runs*. NetPace's rule changes:

- `Bash(rm:*)` and `Bash(rmdir:*)` came off `ask` ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)).
- `Bash(git push:*)` moved to `allow`, so `/raise-pr` pushes without stopping; `Bash(chmod:*)` moved from `deny` to `ask` ([CIR](change-intent-records/2026-09-04-push-allow-chmod-ask.md)).
- The six `Read(…)` deny rules were removed: their glob scope made every recursive read escalate to an approval no mode auto-grants ([CIR](change-intent-records/2026-09-04-read-deny-rules-removed.md)).

`chmod` is the only `ask` rule, so it is the one call a chained stage could lose silently — and the executable-bit forms no longer do. [`no-chmod.sh`](../.claude/hooks/no-chmod.sh) refuses them first with a message naming `bash script.sh`, so the agent is redirected rather than stalled or quietly degraded ([CIR](change-intent-records/2026-09-27-refuse-executable-bit-chmod.md)). The `ask` rule stays in place as the fail-open backstop for the forms the hook declines to decide.

## Chain

The generic *Running the stages end to end*, *The chain script* and *The chain runner* say what a chain and a runner must do. This section is the runbook for NetPace's two scripts: how each is invoked, configured, operated and tested.

Why `scripts/chain.sh` opens the PR without a pause: [CIR](change-intent-records/2026-09-14-chain-raises-pr-unattended.md). Why it no longer studies: [CIR](change-intent-records/2026-10-09-study-removed.md).

### The chain script

[`scripts/chain.sh`](../scripts/chain.sh) runs the three stages for one issue.

- **Invocation.** `scripts/chain.sh <issue>` (bare or `#`-prefixed). `scripts/chain.sh --dry-run <issue>` lists the three stages and the command each would send, and runs nothing — no git command, no model.
- **Prerequisites.** `git`, `claude`, `gh`, `jq` and `timeout` on PATH; `claude` and `gh` signed in; a clean checkout of `main`. The chain checks the five tools, that an issue was named, the clean tree, `main`, and that `CHAIN_STAGE_TIMEOUT`, if set to a value, is a whole number **above zero** — zero reaches `timeout` as *no limit*, switching off the stage's only stall detector; `/build` checks the fetch, unpushed commits and the issue.
- **Configuration.** `CHAIN_MODEL` (default `claude-opus-5`) is the model for every stage. Per-stage time limits are build 2h, verify 2h, raise-pr 30m; `CHAIN_STAGE_TIMEOUT` (seconds) overrides all three, for tuning from real runs, and is refused at zero. Verify's 2h is sized from a measured run: on #268's branch, about 2,400 added lines, the review and its fixes took about 40 minutes from start to commit. The limit is the stage's only stall detector, so it is left at roughly three times that and no more.
- **When a stage fails.** The closing message names the stage, its position (`[2/3]`) and the reason: the stage's own `FAILED reason=`, `the stage reported a failure with no reason`, `no readable verdict — the last line of the report is not a verdict`, `claude reported an error`, `reply was not JSON`, `reply was not a JSON object`, `claude exited with <code>`, `the stage could not be launched (exit <code>)`, `killed (exit 137) after <n>s, before its <n>s time limit`, or `stalled — exceeded <n>s`. Its second line gives `claude --resume <id>` for the failed stage, if the reply carried an id; otherwise it says to reopen the most recent headless session for the repo. Diagnose there, then run the remaining stages by hand, in order.
- **What the stopped run left behind.** Immediately *before* those two lines the chain prints the commits the current branch holds over `main` — or that it holds none — and whether the working tree has uncommitted changes. That is the state to read before continuing by hand: a stage killed at its time limit leaves no report of its own, and a stage stopped part-way can leave commits or uncommitted edits behind. The lines are `git log`/`git status` output and change nothing. They sit before those two lines so the closing lines stay the last thing in the output, where [`chain.tests.sh`](../scripts/chain.tests.sh) and a reader both look for them; the parked-issue comment carries the stage and reason only, not this state.
- **Reading the verdict.** The verdict is the report's **last non-blank line**, and no other line is read (#345). Whatever a report quotes above that line — RED evidence in a fence, a reviewer's own `FAILED reason=`, prose reading `not VERIFIED branch=…` — changes nothing, in either direction. All three prompts therefore end on their verdict, and `verify.md` writes both templates bare rather than fenced: a fence closing beneath the verdict would be the last line.

  On that one line the chain strips **one** leading heading, blockquote, bullet or numbered-item marker — one, not a stack, so `> - FAILED reason=…` is not a verdict — and a run of `*`, `_` or backticks that *wraps* the line, the closing run only when the same run closes the line it opened, so a reason legitimately ending in a backtick keeps it (the one exception is an unbalanced wrap, where the single trailing backtick is both the wrapper and the reason's own, and the reason loses that character). It also strips trailing whitespace and an indent of **up to three spaces** — four spaces, or a tab, is markdown's indented code block, and stripping those indiscriminately would let a quotation indent itself into position, the one way a quotation could otherwise fake being last. A trailing carriage return is stripped too, belt-and-braces rather than load-bearing, since `[[:space:]]` already covers it.

  The line must still open with the verdict word and carry its payload key, and for the three success verdicts it must carry **nothing else**: they are anchored at both ends over a non-empty payload (`^READY…branch=<x>$`, `^VERIFIED…branch=<x>$`, `^RAISED…pr=<url>$`), where `FAILED…reason=` runs to the end of the line because its reason is free prose. So `UNVERIFIED branch=…`, `Verdict: FAILED reason=…` and a bare pull-request URL are all **not** verdicts — and neither is `VERIFIED branch=x, FAILED reason=suite red`, `VERIFIED branch=x (2 Important deferred)` or a truncated `VERIFIED branch=`. Anchoring only the *start* was a live false success: the failure test matches `^FAILED` and so cannot see a `FAILED` further along the line, so a stage that hedged its own verdict on one line was read as a pass and carried through to a real pull request. Anything the chain cannot read stops the run as `no readable verdict`, reported distinctly from a stage that failed with a reason, and never retried: a false stop costs the rest of one run, where a false success opens a pull request off unverified work.

  Alternatives weighed and rejected — a stricter prose scan, and a channel outside the prose — are in [CIR 2026-10-09](change-intent-records/2026-10-09-verdict-read-by-position.md). What this replaced, and why none of it is coming back: the chain used to scan the whole report, permissively for `FAILED` and precisely for success. That stopped #333's own run at a `/build` that had succeeded, over a failure verdict the report showed as RED evidence; and four confirmed report shapes passed a **failed** `/verify` through to a raised pull request, because, in three of them, a failure line the scan missed sat above a success-shaped phrase it matched — the fourth wrote no failure line at all, and is caught only because the success word no longer matches mid-line. Dropping fenced blocks before the scan was tried on #333 (`dd1cdfe`) and reverted (`d2addd8`) — an unclosed fence hid correctly written verdicts beneath it. Every added pattern brought its own exceptions; position brought none.
- **Tests.** [`scripts/chain.tests.sh`](../scripts/chain.tests.sh) covers order, malformed and errored replies (including one that is valid JSON but not an object), failure, stall, a stage killed from outside before its limit, refusals, dry run and a stage that rewrites the chain script mid-run, against a stub `claude` in throwaway repos, leaving your checkout untouched. Verdict reading has a case per shape: quoted failure verdicts in fences, lists, tables and quotations above a healthy verdict; each decoration a verdict has been seen wrapped in; the four report shapes that used to pass a failed `/verify` through to a pull request; and the reason surviving a CRLF report, a whitespace-only reason and one that ends in a backtick. Run it after any edit to the chain.

Manual checks, with a real model:

- **Reopening a failed stage's session.** From a clean `main`, force a stall with `CHAIN_STAGE_TIMEOUT=60 scripts/chain.sh <issue>`. Expect `chain: FAILED at [1/3] build — stalled — exceeded 60s`, exit 1, no later stage, and a second line saying no session id was captured (a stalled stage never reports one). `claude --resume`, picking the most recent headless session, should open the stalled `/build`. Remove any branch it left.
- **A full run** against a small ready issue: `scripts/chain.sh <issue>` from a clean `main`. Expect three `ok` lines in order, `chain: done — <pull request URL>`, exit 0, no prompt at any point, and a clean working tree.

### The chain runner

[`scripts/chain-next.sh`](../scripts/chain-next.sh) runs on the build VPS, fired every 15 minutes by a systemd user timer, with lingering already enabled for the build user. The shipped unit files in [`scripts/systemd/`](../scripts/systemd/) work there unedited. `ready` is the only opt-in, so confirming an issue queues it. There is no separate queue label. It supersedes the dispatcher proposed in #266.

How NetPace meets the generic runner rules:

- **One run at a time.** systemd never starts an active service twice, and the runner also holds a `flock` lock, so running the script directly cannot overlap the timer's run either.
- **A clean start.** The clone is forced onto `origin/main` and cleaned with `git clean -fdx`, which takes `bin/`, `obj/` and `.claude/scratch/` with it. The runner refuses any directory not marked `chain-next.dedicated`.
- **Tracker.** GitHub, read through `gh`; "blocking issue" means GitHub's native issue dependencies.
- **Report mode.** `scripts/chain-next.sh --dry-run`.

**Prerequisites** on the build machine: everything `scripts/chain.sh` needs (`git`, `claude`, `gh`, `jq`, `timeout`, `claude` and `gh` signed in non-interactively), plus `dotnet`, `flock` (util-linux) and systemd with lingering enabled for the build user (`loginctl enable-linger`), so the timer survives logout and reboot.

**One-time setup:**

1. Clone the repository somewhere used only by the runner, with a full `git clone` rather than a worktree. Mark it as the runner's:

   ```bash
   git clone https://github.com/FrankRay78/NetPace.git ~/Repos/NetPace-runner
   git -C ~/Repos/NetPace-runner config chain-next.dedicated true
   ```

   If the build user has no global `user.name` and `user.email`, set them in this clone with `git -C ~/Repos/NetPace-runner config user.name "<name>"` and the same for `user.email`.
2. Create the `parked` label once: `gh label create parked --description "The chain runner stopped on this issue; remove to retry"`.
3. Check what the first firing would build. This only reads GitHub, so it also confirms `gh` access, and it changes nothing:

   ```bash
   bash ~/Repos/NetPace-runner/scripts/chain-next.sh --dry-run
   ```

   Set `CHAIN_NEXT_CLONE` first if the clone is not at the default path. The issue it names is built, and a real pull request opened, as soon as the timer is enabled in the next step.
4. Install the schedule. Copy the unit files:

   ```bash
   mkdir -p ~/.config/systemd/user
   cp ~/Repos/NetPace-runner/scripts/systemd/netpace-chain-next.{service,timer} ~/.config/systemd/user/
   ```

   Check three lines in the copied service: `CHAIN_NEXT_CLONE`, the `ExecStart` path and `PATH`. User units start with a minimal environment, so `PATH` must name where `claude`, `dotnet`, `gh`, `jq`, `git`, `flock` and `timeout` live. The shipped values suit a clone at `~/Repos/NetPace-runner` with those tools in `~/.local/bin`, `~/.dotnet` or `/usr/bin`. Then start it:

   ```bash
   systemctl --user daemon-reload
   systemctl --user enable --now netpace-chain-next.timer
   ```

**Configuration.** `CHAIN_NEXT_CLONE` is the dedicated clone (default `~/Repos/NetPace-runner`). `CHAIN_NEXT_STATE_DIR` holds `logs/` and the lock, outside the clone so the clean cannot delete them (default `${XDG_STATE_HOME:-~/.local/state}/netpace-chain`). `CHAIN_MODEL` and `CHAIN_STAGE_TIMEOUT` pass through to the chain.

**Operating it:**

| To | Run |
|---|---|
| Start (and on every boot) | `systemctl --user enable --now netpace-chain-next.timer` |
| Pause (a run in progress finishes) | `systemctl --user stop netpace-chain-next.timer` |
| Stop a run in progress too (the issue is not parked) | `systemctl --user stop netpace-chain-next.service` |
| Resume | `systemctl --user start netpace-chain-next.timer` |
| Fire once now | `systemctl --user start --no-block netpace-chain-next.service` |
| See status and the next firing | `systemctl --user status netpace-chain-next.service` and `systemctl --user list-timers netpace-chain-next.timer` |
| Read the runner's own output | `journalctl --user -u netpace-chain-next.service` |
| Read one run's full chain output | `logs/<N>-<timestamp>.log` in the state directory (see *Configuration*) |
| Retry a parked issue | Remove its `parked` label; the next firing picks it up |

**Tests.** [`scripts/chain-next.tests.sh`](../scripts/chain-next.tests.sh) covers selection (closed, not-ready, parked, blocked and already-raised issues), report mode, a clean start after a previous run, a reset that rewrites the runner's own source, parking and un-parking, kept attempt branches (including one whose pushed twin was removed, and one that cannot be set aside), overlapping firings, back-to-back runs, fail-closed queries (each lookup failing on its own), machine faults that park nothing and the dedicated-clone guard. It uses a stub `gh` and a stub `chain.sh` in throwaway repositories, so no model or GitHub call is made. Run it after any edit to the runner.

Manual checks, on the build machine:

- **Report against the real repository.** `scripts/chain-next.sh --dry-run` names the lowest-numbered eligible issue, matching what `gh issue list --label ready` and the issues' blockers and linked pull requests show, and leaves the clone, labels and comments unchanged.
- **The installed timer.** After `enable --now`, `systemctl --user list-timers` shows the next firing, and `journalctl --user -u netpace-chain-next.service` shows `chain-next: nothing eligible.` (or a run) after it fires.

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
