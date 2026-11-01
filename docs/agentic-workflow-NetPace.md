# Agentic Workflow — the NetPace delta

Companion to the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin's [workflow guide](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/workflow.md), the stack-neutral generic guide to the workflow its commands implement. This file records **only where NetPace differs** from it or makes it concrete; anything not listed here follows the generic guide as written. NetPace-specific behaviour never edits the generic guide: a change to it, or to a command, is an issue or pull request on gaffer.

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

**Where the commands come from.** The four Claude workflows run the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin's commands. Each job clones gaffer's `main` into the runner's temporary directory in a step of its own — the URL and ref written in the workflow, the commit printed to the log — passes the clone to Claude as `--plugin-dir` and `--add-dir`, and tells the agent to read the command file from that clone and apply it as written, with the environmental deltas the prompt lists. Nothing in a pull request's head can choose which gaffer runs. A change to a command therefore takes effect on its merge in gaffer, not here; a change to `REVIEW.md` or `CONVENTIONS.md` still takes effect on merge here, because the workflows put `main`'s copy in place before the agent starts. Rationale: CIR [`2026-11-01-netpace-runs-gaffers-commands`](change-intent-records/2026-11-01-netpace-runs-gaffers-commands.md).

**Every review round runs in CI, and says what it cost.** The first review runs on PR open; labelling `review-followup` runs the next round and the label comes off when the run ends, so adding it again starts the one after. The cap is unchanged at three rounds, and a label added past it, or on a PR with no first review yet, is a green run that posts nothing. Each round that posts adds a row — `PR Review`, then `Review 1`, `Review 2` — to a table at the foot of the PR description, with a `Total` row under it, so a merged PR carries the cost of reviewing it. The rows come from [`scripts/pr-cost-footer.sh`](../scripts/pr-cost-footer.sh); a round run by hand from `/gaffer:review-pr` or `/gaffer:review-followup` locally still works and adds no row. Rationale: the [cost-footer CIR](change-intent-records/2026-10-28-review-rounds-record-what-they-cost.md).

**The whole pre-build issue gate runs in CI.** Every step is label-triggered, so an issue raised away from the desk can be reviewed, answered and confirmed without a checkout. Labelling `review` runs gaffer's [`reviewissue`](https://github.com/FrankRay78/gaffer-claude/blob/main/commands/reviewissue.md) command and leaves the gap analysis as a comment; the author answers it inline in the GitHub UI; where an answer asks for help rather than deciding, labelling `refine` runs step 6 of the same command and re-frames just those questions in place; labelling `confirm` then runs gaffer's [`confirmissue`](https://github.com/FrankRay78/gaffer-claude/blob/main/commands/confirmissue.md), which folds the answers into the issue body and deletes the comment. Each workflow applies its command as written, keeping the logic single-sourced. Both commands still run locally.

**One label per issue state.** Each of the three does exactly one job, and which one fits is decided by where the issue stands:

| Issue state | Label that fits | What it does |
|---|---|---|
| No review yet | `review` | Posts the gap analysis |
| Review posted, answers not yet written | none | Answer the questions first |
| Review posted, at least one answer asks for help | `refine` | Re-frames only those questions, in place |
| Review posted, answers concrete | `confirm` | Folds the answers into the body and marks it `ready` |
| `ready` | none | Nothing left to do |

A label applied in any other state is a **wrong-state trigger** — `review` on an issue that already has a review or is `ready`; `refine` on one with no review, one that is `ready`, or one whose answers are all concrete or all blank; `confirm` on one with no review or already `ready`. A wrong-state trigger changes nothing on the issue and removes the label it was triggered by, so the right one can be applied straight away. The run finishes green; it does not fail and it does not say which label would have fitted. Removing the label is what makes that recoverable from the issue page alone.

Two states are *not* wrong-state triggers, and both go red with the label left on: `ready` on an issue that still carries a review comment, which is a half-confirmed issue needing manual repair rather than a mislabelling; and `confirm` on a review whose answers are still unanswered or hedging, which the confirm command stops on by design. So a mislabelled `confirm` is a cheap no-op in two states and a red run in two others.

Refine — by the label or by `/gaffer:reviewissue` locally — reads the comments posted after the review, and uses any that an answer points at when re-framing that question, since an answer slot is one quoted line and an author with more to say writes it underneath.

The three paths share one per-issue concurrency group, `issue-<n>`. Concurrency groups are repo-wide, so the shared *name* is what stops a review posting on top of a confirmation in flight, or a refine rewriting the comment a confirmation is reading.

**The labels are the state** — the generic *Context management* rule, made concrete. A green run always removes the label that triggered it. So:

- **`review` labelled** — a review is pending; if it stays labelled, the run did not complete.
- **`needs answers`, with a `<!-- speckit:review -->` comment** — a review is posted and awaiting answers.
- **`refine` labelled** — a refine is pending; if it stays labelled, the run did not complete. `needs answers` stays on throughout: a re-framed question is still a question.
- **`confirm` labelled** — a confirmation is pending; if it stays labelled, the run did not complete. On one of the command's hard-stops the issue is untouched and the answers are still there to fix and re-label; on a failed post-condition the decisions may already be on the body with a half-applied set of labels. The run summary is what tells the two apart.
- **`ready`, with a `## Confirmed decisions` section** — the gate is finished. `ready` takes precedence: an issue confirmed before these changes carries both markers and is finished, not waiting.

**A merge ends the chain.** The states above describe open issues. When a pull request merges, `clear-workflow-labels.yml` takes every one of these labels off each issue it closed, so a label view is not answered by issues the merge already finished. An issue closed any other way keeps its labels. Rationale: CIR [`2026-10-18-merge-clears-issue-workflow-labels`](change-intent-records/2026-10-18-merge-clears-issue-workflow-labels.md).

There is no failure comment on any path: GitHub's failed-run notification is the alert, and all three paths check their own post-conditions. The refine path checks what holds however much it re-framed — the review comment is still there, the issue body is untouched, and the `refine` label came off — because the agent removes that label only once its edit has landed or it has concluded nothing qualifies, so a surviving label means the run did not finish. What it deliberately does *not* check is whether the comment changed: a refine run that correctly changes nothing, because every answer is concrete, leaves it byte-for-byte as a run that should have re-framed a question and did not, so that check would fail the commonest correct outcome. The confirm workflow checks five — decisions on the body, `ready` on, `needs answers` off, no surviving review comment, and the agent's report written — because three operations across the command's steps 6 and 7 are never fatal, and without that check a half-finished confirmation would go green. It names every missing half in one run, and puts the command's report in the run summary, which on a stop is the only place the outstanding gaps appear.

To re-review a confirmed issue, remove `ready` first, and delete any review comment still on the thread — with both markers gone the review command treats it as a first run. An issue confirmed before `/gaffer:confirmissue` began deleting the review carries its old comment, and the review guard reads that as a wrong-state trigger and skips. One gap on all three paths: a label applied by any account other than the gated one creates no run at all, so the issue stays labelled but silent. Rationale and residuals: CIRs [`2026-09-07-automated-prespec-review`](change-intent-records/2026-09-07-automated-prespec-review.md), [`2026-09-11-confirmed-decisions-replace-the-review`](change-intent-records/2026-09-11-confirmed-decisions-replace-the-review.md), [`2026-09-26-automated-prespec-confirmation`](change-intent-records/2026-09-26-automated-prespec-confirmation.md).

## Release pipeline

NetPace ships `NetPace.Core` as a **NuGet package** and cross-platform **binaries** (6 RIDs × self-contained/framework-dependent) on tag push. The generic guide has no release step; for NetPace it is first-class.

The contract — release matrix, runner-per-RID rationale, naming convention, smoke-test and size-assertion contracts — lives in [RELEASING.md](RELEASING.md). Touching `release-binaries.yml` (or other release-pipeline scope) without updating it is a documented no-no (`CONVENTIONS.md` → *Documentation*). Release notes are GitHub-auto-generated from merged PRs; there is no `CHANGELOG.md`.

## Decision ledger: Change-Intent-Records **and** memory

The generic guide's *Documentation hierarchy* leaves the detailed-reference tier to the repository; NetPace fills it with **two ledgers, for different jobs**:

- **Change-Intent-Records** — dated `YYYY-MM-DD-slug.md` files in [`docs/change-intent-records/`](change-intent-records/): the human-facing record of *why* a non-obvious change was made (the AOT release shape, the profile CLI switch, the automated issue gate, supply-chain hardening). When to write one: [`docs/conventions/change-intent-records.md`](conventions/change-intent-records.md).
- **Memory** — [`.claude/memory/`](../.claude/memory/), indexed by `MEMORY.md` and loaded via `CLAUDE.md`: agent-facing facts and corrections, one per file. "Prefer a gate to a memory entry" is live here: several memories survive only as the *rationale* for a gate that now enforces them (`feedback_trusting_a_test_run` → `green-gate.sh`; the skip ban → `no-skipped-tests.sh`).

## The gates, concretely

The generic enforcement layer as NetPace wires it. Hooks live in [`.claude/hooks/`](../.claude/hooks/), are wired in `.claude/settings.json`, and are documented in [`.claude/hooks/README.md`](../.claude/hooks/README.md).

| Generic gate | NetPace implementation | Event |
|---|---|---|
| Stale-build guard | `green-gate.sh` — denies `dotnet test --no-build` when no test assembly is built or a `*.cs` under `src/` is newer than it | PreToolUse(Bash) |
| No skipped tests | `no-skipped-tests.sh` — blocks any `git commit` while a skip-family construct (incl. xUnit-v3 `SkipUnless=`/`SkipWhen=`) exists under `src/`; a `--check` mode exists, but no workflow runs it yet | PreToolUse(Bash) |
| PR pre-flight | `dotnet build ./src && dotnet test ./src` before `gh pr create` | PreToolUse(Bash), `if gh pr create` |
| **Formatting** | **`/gaffer:verify`'s formatting passes — `dotnet format ./src/NetPace.sln`, the bare command CI checks: once up front (step 1a) and again if the review applied edits (step 3). Not a hook** (see below) | — |
| **Formatting, gated** (no generic counterpart) | **`dotnet format ./src/NetPace.sln --verify-no-changes` in [`dotnet.yml`](../.github/workflows/dotnet.yml) — the check `/gaffer:verify` formats against. Not a hook, and no script:** the step is inline in the workflow | pull_request → main |
| **Test-green gate** | **`/gaffer:verify`'s suite gate (step 1b) — a real `dotnet build ./src && dotnet test ./src`. Not a hook.** | — |
| **Scenario traceability** | **`scripts/traceability-check.sh` — fails naming any `**Scenario: X**` label on the branch's issue with no matching `SCENARIO: X` marker in a committed test file. Not a hook:** the [`traceability.yml`](../.github/workflows/traceability.yml) `traceability` job runs it on every PR, and agents run it locally before raising one | pull_request → main |
| **PR links an issue** (no generic counterpart) | **the `issue-link` job in [`pr-issue-link.yml`](../.github/workflows/pr-issue-link.yml) — asks GitHub which issues merging the PR closes and fails unless one is open and in this repository. Not a hook, and no script:** the check is inline in the workflow | pull_request → main |
| **Repository references resolve** (no generic counterpart) | **`scripts/link-check.sh` — fails naming any markdown link in a tracked `*.md`, or any bare `docs/change-intent-records/<name>.md` citation in a tracked text file, that names a file the repository does not hold, or any link naming a heading its target file does not have. A full `https://github.com/FrankRay78/NetPace/{blob,tree,raw}/main/<path>` link is mapped onto `<path>` in the checkout, so no link is ever fetched. Not a hook:** the [`link-check.yml`](../.github/workflows/link-check.yml) `link-check` job runs it on every PR | pull_request → main |
| Fast/slow test categories | none: the suite is fully mocked and fast, so there is no split | — |

Every hook has an **announced override** (`NETPACE_SKIP_GREEN_GATE=1`, `NETPACE_ALLOW_SKIPS=1`). `green-gate.sh` fails open. `no-skipped-tests.sh` fails closed once a call is classified as a `git commit`, since a skip ban that fails open is the silent non-coverage it exists to stop.

`traceability-check.sh` is not a hook and has **no override**: it fails closed on a missing tool, an unresolvable repository, an auth, network or rate-limit failure, a reply whose body it cannot read, an issue body with an unterminated code fence, and a marker scan that cannot complete — because a merge gate that passes without having run gives exactly the false comfort it exists to remove. The quiet passes are enumerated rather than incidental: GitHub answering "nothing at that number" is an answer rather than an outage, so a branch with no issue number, a number that is a pull request, a number that resolves to nothing, and an issue carrying no labels each pass with nothing to check — and each says which of those it is, so the four are distinguishable in a log. Its `traceability` job is a fixed-name context and a required check in the `Main CI/CD` ruleset, so a failure blocks the merge.

`link-check.sh` is not a hook and has **no override**, and fails closed for the same reason: a missing tool, a directory that is not a git repository, finding no tracked `*.md` at all, a file with an unterminated code fence, and a linked-into markdown file whose headings cannot be read are all failures rather than clean passes. It judges the **working tree** rather than `HEAD`, so a local run is usable midway through a deletion, before anything is committed; the file list still comes from `git ls-files`, so an untracked scratch file never gets scanned — though target existence is a plain filesystem test, so a link resolving to an untracked file passes locally and fails in CI. **Three shapes of link target are resolved, and none of them is fetched:** a relative or root-relative path, from the linking file's own directory as GitHub renders it; a full `https://github.com/FrankRay78/NetPace/{blob,tree,raw}/main/<path>` link, mapped onto `<path>` in the checkout, so a link written the way a reader copies it off the GitHub page is still answered for by this branch's own files; and an `#anchor`, matched against the ATX headings of the markdown file it lands in by GitHub's own slug — lowercase the text, drop everything that is not a letter, digit, hyphen or underscore, turn each remaining space into a hyphen, and count `-1`, `-2`, … onto the second and later occurrences of a repeated heading. A link inside a heading contributes its text and not its target, as on GitHub. Four kinds of heading are read differently: a non-ASCII **letter** is dropped where GitHub keeps it; a **setext** heading (`Text` over `====`) is not seen as a heading at all, nor is an ATX heading **indented** by one to three spaces, nor an explicit `<a id="...">` anchor. None of the four occurs in the repository today, and each costs a reported-unreachable link rather than a quiet pass. Setext is unread by choice: 32 tracked `*.md` files open with `---` front matter, whose closing delimiter a naive rule would mint a heading from, and a check that invents headings passes links that are broken — worse than one that loudly reports a working link. What the markdown-link half deliberately does not read is a target inside a fenced block or inline backticks — the harness prompts spell out link syntax, and `.claude/memory/MEMORY.md` carries `- [Title](file.md) — hook` as its own format, so reading those would report five dangling links that are nothing of the kind; the bare-citation half strips neither, so a record path is read wherever it appears. Also unread, because none of it names a path in this branch: the rest of github.com (`/releases`, an issue, a wiki page, an action badge), a path on another ref or in another repository, and an anchor on a target that is not a markdown file, such as a `#L10-L20` line range on a source file. Its `link-check` job is a fixed-name context, but **it is not in the `Main CI/CD` ruleset yet**, so today a dangling reference is reported on the PR without blocking the merge; registering it is a change to make by hand in the ruleset, after the merge, because requiring a context whose workflow is not yet on `main` blocks every other open pull request.

The `issue-link` job has **no override** and no parser: it reads `closingIssuesReferences`, GitHub's own answer to "what will merging this close?", so a closing keyword in the body and an issue attached through the Development sidebar both count, and a bare `#N` does not. It fails if GitHub cannot be reached or the reply cannot be read. **`dependabot[bot]` is the only exempt author**; there is no label exemption. It is a fixed-name context, but **it is not in the `Main CI/CD` ruleset yet** — registering it, and deleting the surviving repository-admin bypass actor so the rule binds everyone including Frank, are two changes to make by hand in the ruleset (GitHub → Settings → Rules → `Main CI/CD`; `gh api repos/FrankRay78/NetPace/rulesets/4185732` reads the result back). Until both are done a failure is reported on the PR without blocking the merge, and a direct push to `main` by an admin still succeeds.

Each hook is a **script with a `.tests.sh` case matrix beside it** (generic *Modifying a harness*, rule 1). The exception is the PR pre-flight: an inline command in `settings.json`, so a red suite exits 1, not 2 — it is reported but does not block `gh pr create`. The binding gate is `/gaffer:verify`'s suite run. **Every `*.tests.sh` in the repo runs in CI**: [`shell-tests.yml`](../.github/workflows/shell-tests.yml) discovers them with `git ls-files '*.tests.sh'` rather than naming them, one matrix entry per script with `fail-fast: false`, so a new matrix is gated the moment it is committed and every failure is reported against the script that produced it. Its fixed-name `shell-tests` job is the one stable context branch protection can require; **it is not in the `Main CI/CD` ruleset yet**, so today a red matrix is reported on the PR without blocking the merge.

**Two generic gates do not apply.** There is **no stack-guard** (no external service stack to orchestrate) and **no UI-automation denylist** (a console CLI has no browser UI to guard).

**Console output is verified by snapshot.** `NetPace.Console.Tests` uses `Spectre.Console.Testing` with `Expectations/*.verified.txt` snapshots — how a CLI covers the *verify* duty for rendered output. Check the `*.verified.txt` before reporting an output mode as untested (memory: `feedback_console_output_snapshot_coverage`).

### Formatting

Formatting runs inside `/gaffer:verify`, never on commit — the generic *Formatting is not verification* section, made concrete:

```bash
dotnet format ./src/NetPace.sln
```

**This is the bare command, and that matters: it is exactly what CI runs** as `dotnet format ./src/NetPace.sln --verify-no-changes`. It was previously the narrower `dotnet format style … && dotnet format whitespace …`, which can leave a tree CI's check then rejects — a branch reported verified that fails the format check on its own pull request. The bare command also applies analyzer fixes, so read what it changed rather than assuming it only moved whitespace.

The explicit solution argument is **required**: `dotnet format` only looks in the *current directory*, and NetPace's solution lives under `src/`. Without it the command fails.

**How often.** Once up front (step 1a), and once more if the review applied edits, before the suite re-run (step 3). That is at most two invocations per pull request, not one per commit. Formatting only up front would leave every review fix unformatted, since step 1a runs before any fix exists; formatting *after* the suite re-run would commit bytes the suite never saw.

**Why not per-commit.** Measured when the solution had 84 `.cs` files (~9,900 LOC): **21.4s** for one staged file, **29.5s** for seven. The cost is MSBuild **workspace load**, not file count, so every commit would pay ~20–30s regardless. A release cycle's drift, by contrast, is a handful of import reorderings and a couple of hundred trailing spaces on blank lines — nothing a reviewer would catch. The guide's "tens of seconds" holds even at this size, so "our solution is small enough to absorb it" does not survive.

**Line endings.** `.gitattributes` pins `*.cs text eol=lf`, matching `.editorconfig`'s `end_of_line = lf` and the LF the index stores. Without it, a Windows checkout with `core.autocrlf=true` gets a CRLF working tree, and `dotnet format whitespace` rewrites every file it touches — no committed diff, since commit normalises back, but thousands of phantom findings drowning the real ones. A Windows working tree created *before* the attribute needs a one-time refresh (re-clone, or `git rm --cached -r . && git reset --hard` on a clean tree); fresh clones and Linux checkouts are unaffected.

## `/gaffer:build`

Follows the generic *build stage*. NetPace's specifics:

- **Branch:** `feature/<N>-<short-slug>`, cut from `origin/main`. `/gaffer:raise-pr`, `/gaffer:verify`, `scripts/traceability-check.sh` and `scripts/chain-next.sh` all read the issue number from this pattern.
- **Commits:** `Refs #<N>: …` in the imperative mood (`CONVENTIONS.md` → *Commit messages*). The failing tests are committed with the implementation that turns them green, never on their own.
- **Suite:** as in *The gates, concretely*.
- **Docs it must update** (`CONVENTIONS.md` → *Documentation*): `///` XML docs on any new or changed public `NetPace.Core` API; the README.md `--help` snapshot and USER_GUIDE.md for a changed CLI option; `docs/RELEASING.md` for a release-pipeline change.
- **Change-Intent Record:** not a documentation obligation, and most changes get none. `/gaffer:build` runs the decision table in [`conventions/change-intent-records.md`](conventions/change-intent-records.md) and writes a record only where a `→ CIR` row applies and no `→ No CIR` row does, naming the warranting row in the commit and its final report — or reporting that none was warranted. Where a record exists, `/gaffer:raise-pr` links it from the pull request body instead of repeating its reasoning.

## `/gaffer:verify`

Follows the generic *verify gate*. NetPace's specifics:

- **Steps 1a/1b:** the formatting and test-green rows in *The gates, concretely*.
- **Review A (steps 2–3):** one round over the whole branch diff, in two waves of the applicable reviewers. Wave 1: the five report-only `pr-review-toolkit` reviewers and `/gaffer:review-slop`, together. Wave 2: `pr-review-toolkit:code-simplifier`, which edits files, alone. Confirmed Blockers and Importants in the branch's own code are fixed with the smallest change that removes each, and committed on a green re-run. Nothing inside `/gaffer:verify` reviews that commit; Review B reads it on the pull request (generic *Review once*, and gaffer's `verify` command, step 2).

## `/gaffer:audit-codebase`

Follows the generic *codebase audit* periodic task. NetPace's specifics:

- **Argument:** a tag or commit that starts the "what changed" window, normally the last release tag. With none, the command asks and stops.
- **Lenses:** conformance to the stated rules, test-suite integrity, docs against code, harness consistency, and dead code and duplication.
- **Evidence gathered first:** the build and suite with coverage (`dotnet-coverage`), `dotnet list package --deprecated` and `--outdated`, `nuget-license`, Roslynator's unused-symbol search and `jscpd`. The four that are not part of the SDK run one-shot at a pinned version (`dotnet tool exec`, `npx`), so the command adds no tool manifest. A tool that cannot run is named in the report rather than stopping the audit.
- **Output:** one issue labelled `housekeeping`, never `ready`, `review` or `confirm`, so neither the chain runner nor the issue-review workflows pick it up.
- **Not covered:** security and mutation testing (gaffer's [`audit-codebase`](https://github.com/FrankRay78/gaffer-claude/blob/main/commands/audit-codebase.md) command says why).

## Permissions

The mechanism is in the generic *Permissions and unattended runs*. NetPace's rule changes:

- `Bash(rm:*)` and `Bash(rmdir:*)` came off `ask` ([CIR](change-intent-records/2026-09-04-rm-off-the-ask-list.md)).
- `Bash(git push:*)` moved to `allow`, so `/gaffer:raise-pr` pushes without stopping; `Bash(chmod:*)` moved from `deny` to `ask` ([CIR](change-intent-records/2026-09-04-push-allow-chmod-ask.md)).
- The six `Read(…)` deny rules were removed: their glob scope made every recursive read escalate to an approval no mode auto-grants ([CIR](change-intent-records/2026-09-04-read-deny-rules-removed.md)).

`chmod` is the only `ask` rule, so it is the one call a chained stage could lose silently — and the executable-bit forms no longer do. Gaffer's [`no-chmod` hook](https://github.com/FrankRay78/gaffer-claude/blob/main/hooks/no-chmod.sh), which runs here because the plugin is enabled, refuses them first with a message naming `bash script.sh`, so the agent is redirected rather than stalled or quietly degraded. The `ask` rule stays in place as the fail-open backstop for the forms the hook declines to decide.

## Chain

The generic *Running the stages end to end* and *A scheduled runner* say what a chain and a runner must do; the plugin ships neither, so both are NetPace's. This section is the runbook for its two scripts: how each is invoked, configured, operated and tested.

Why `scripts/chain.sh` opens the PR without a pause: generic *Running the stages end to end*. Why it no longer studies: [CIR](change-intent-records/2026-10-09-study-removed.md).

### The chain script

[`scripts/chain.sh`](../scripts/chain.sh) runs the three stages for one issue.

- **Invocation.** `scripts/chain.sh <issue>` (bare or `#`-prefixed). `scripts/chain.sh --dry-run <issue>` lists the three stages — `/gaffer:build`, `/gaffer:verify` and `/gaffer:raise-pr` — the command each would send and the plugin clone it would load, and runs nothing — no git command, no model.
- **Prerequisites.** `git`, `claude`, `gh`, `jq` and `timeout` on PATH; `claude` and `gh` signed in; a clean checkout of `main`; a clone of the [gaffer](https://github.com/FrankRay78/gaffer-claude) plugin at `CHAIN_GAFFER` (default `~/Repos/gaffer-runner`), which the chain loads every stage from with `--plugin-dir` so a headless run never depends on the copy installed on the machine. The chain checks the five tools, that an issue was named, the clean tree, `main`, that `CHAIN_STAGE_TIMEOUT`, if set to a value, is a whole number **above zero** — zero reaches `timeout` as *no limit*, switching off the stage's only stall detector — that the plugin clone exists, and that the stage table answers all three stages, refusing with exit code 78 for either of the last two, which the runner does not park an issue for; `/gaffer:build` checks the fetch, unpushed commits and the issue.
- **Configuration.** Each stage takes its model and reasoning effort from the stage table, [`scripts/stage-models.tsv`](../scripts/stage-models.tsv), the one place every unattended runner — this chain and the Claude workflows — reads them from; `CHAIN_MODEL` overrides every stage's model, and the effort stays the table's. `CHAIN_GAFFER` names the plugin clone; the chain reads it and never updates it — the runner does, before each firing. Per-stage time limits are build 2h, verify 2h, raise-pr 30m; `CHAIN_STAGE_TIMEOUT` (seconds) overrides all three, for tuning from real runs, and is refused at zero. Verify's 2h is sized from a measured run: on a measured branch of about 2,400 added lines, the review and its fixes took about 40 minutes from start to commit. The limit is the stage's only stall detector, so it is left at roughly three times that and no more.
- **When a stage fails.** The closing message names the stage, its position (`[2/3]`) and the reason: the stage's own `reason` from its failure verdict, `the stage reported a failure with no reason`, `no readable verdict — <what was wrong with the end of the report>`, `the verdict has schema version <v>, which this chain does not read`, `claude reported an error`, `reply was not JSON`, `reply was not a JSON object`, `claude exited with <code>`, `the stage could not be launched (exit <code>)`, `killed (exit 137) after <n>s, before its <n>s time limit`, or `stalled — exceeded <n>s`. Its second line gives `claude --resume <id>` for the failed stage, if the reply carried an id; otherwise it says to reopen the most recent headless session for the repo. Diagnose there, then run the remaining stages by hand, in order.
- **When the usage limit stops a stage.** A stage the account's own Claude usage limit cut off is not a fault in the issue, and the chain reports it as its own thing: the reason is `the Claude usage limit was reached — <the reply's own message>`, which carries the reset time in the reader's timezone (`You've hit your session limit · resets 3:20pm (Europe/Berlin)`) quoted verbatim and unconverted, or `the Claude usage limit was reached; the reply gave no reset time` when the reply carried no message. The chain exits **75** rather than 1, so [`chain-next.sh`](../scripts/chain-next.sh) can tell that stop from every other failure without reading the prose. It is recognised from the reply's typed `api_error` field alone — claude sets that to `usage_limit_reached` only when the account's own limit refused the request, and no other refusal sharing its `rate_limit` error sets it. Never from the report text, for the same reason the verdict is read by position: a report that quotes a limit message it merely read about is not a stage the limit stopped. The check runs before the exit code is dispatched on, because the limit leaves an ordinary exit 1 behind: before this, a limit stop was parked saying only `claude exited with 1`, and its cause had to be dug out of the session transcript by hand.
- **What the stopped run left behind.** Immediately *before* those two lines the chain prints the commits the current branch holds over `main` — or that it holds none — and whether the working tree has uncommitted changes. That is the state to read before continuing by hand: a stage killed at its time limit leaves no report of its own, and a stage stopped part-way can leave commits or uncommitted edits behind. The lines are `git log`/`git status` output and change nothing. They sit before those two lines so the closing lines stay the last thing in the output, where [`chain.tests.sh`](../scripts/chain.tests.sh) and a reader both look for them; the parked-issue comment carries the stage and reason only, not this state.
- **Reading the verdict.** The verdict is the JSON object [`verdict-schema.md`](verdict-schema.md) describes, read from **what ends the report** — the last non-blank line, or the body of a code fence that closes the report — and nothing else in the report is read. Whatever a report quotes above that — RED evidence in a fence, a failure verdict a reviewer raised, a success verdict named mid-sentence — changes nothing, in either direction. All three prompts therefore end on their verdict, bare on one line, and say that a fence around it is tolerated and a sentence after it is not.

  Around the object the chain strips **one** leading heading, blockquote, bullet or numbered-item marker — one, not a stack — a run of `*`, `_` or backticks that *wraps* the line, trailing whitespace, a trailing carriage return, and an indent of **up to three spaces** — four spaces, or a tab, is markdown's indented code block, and stripping those indiscriminately would let a quotation indent itself into position, the one way a quotation could otherwise fake being last; `jq` forgives leading whitespace, so the bare line is also required to open with the object's own brace. A fenced verdict is read from the whole fence body, so a fence holding more than the one object is unreadable rather than read for its last line.

  What is left must be one JSON object, and the schema version is read before anything else: a version this chain does not know stops the run naming it, without reading a reason or a payload out of a shape it does not understand. Then the outcome — `failed` with its reason, or exactly this stage's success outcome with its payload — and fields the chain does not know are ignored. So `Verdict: {…}`, `{…} (2 Important deferred)`, a bare pull-request URL, an outcome of `unverified` or of another stage's success, a success verdict carrying a `reason`, a `branch` that is empty or holds whitespace, a `pr` that is not a bare pull request URL, and the retired text verdicts are all **not** verdicts. Anything the chain cannot read stops the run as `no readable verdict — <what was wrong>`, reported distinctly from a stage that failed with a reason, and never retried: a false stop costs the rest of one run, where a false success opens a pull request off unverified work.

  Why position, and the alternatives weighed and rejected — a stricter prose scan, and a channel outside the prose — are in [CIR 2026-10-09](change-intent-records/2026-10-09-verdict-read-by-position.md); why the verdict became a versioned object, and why a fence that closes the report now counts, in [CIR 2026-10-30](change-intent-records/2026-10-30-verdicts-are-versioned-json.md).
- **Tests.** [`scripts/chain.tests.sh`](../scripts/chain.tests.sh) covers order, the plugin clone each stage is loaded from and the refusal without one, malformed and errored replies (including one that is valid JSON but not an object), failure, stall, a stage killed from outside before its limit, refusals, dry run and a stage that rewrites the chain script mid-run, against a stub `claude` in throwaway repos, leaving your checkout untouched. Verdict reading has a case per shape: quoted verdicts in fences, lists, tables, quotations and sentences above a healthy verdict or above closing prose; each decoration a verdict has been seen wrapped in, fences included; an unknown schema version, a version written as a string, and fields the chain does not know on a success and on a failure; the retired text verdicts; the hedged shapes that used to pass a failed `/gaffer:verify` through to a pull request; a missing, empty or malformed payload for each outcome; and the reason surviving a CRLF report, outer whitespace, quotes and backslashes, and a backtick-wrapped line. A usage-limit stop has cases of its own: the exit code and the quoted reset wording, a limit reply carrying no message, a healthy report and a failing one that each merely quote the limit wording, and a stage that exits non-zero with no limit in its reply. [`scripts/stage-flags.tests.sh`](../scripts/stage-flags.tests.sh) covers the stage table's lookup and checks that every chain stage and Claude workflow reads its model from the table. Run both after any edit to the chain or the table.

Manual checks, with a real model:

- **Reopening a failed stage's session.** From a clean `main`, force a stall with `CHAIN_STAGE_TIMEOUT=60 scripts/chain.sh <issue>`. Expect `chain: FAILED at [1/3] build — stalled — exceeded 60s`, exit 1, no later stage, and a second line saying no session id was captured (a stalled stage never reports one). `claude --resume`, picking the most recent headless session, should open the stalled `/gaffer:build`. Remove any branch it left.
- **A full run** against a small ready issue: `scripts/chain.sh <issue>` from a clean `main`. Expect three `ok` lines in order, `chain: done — <pull request URL>`, exit 0, no prompt at any point, and a clean working tree.

### The chain runner

[`scripts/chain-next.sh`](../scripts/chain-next.sh) runs on the build VPS, fired every 15 minutes by a systemd user timer, with lingering already enabled for the build user. The shipped unit files in [`scripts/systemd/`](../scripts/systemd/) work there unedited. `ready` is the only opt-in, so confirming an issue queues it, unless someone other than the maintainer or the review bot raised it or has commented on it. There is no separate queue label. It supersedes the dispatcher proposed in #266.

How NetPace meets the generic runner rules:

- **One run at a time.** systemd never starts an active service twice, and the runner also holds a `flock` lock, so running the script directly cannot overlap the timer's run either.
- **A clean start.** The clone is forced onto `origin/main` and cleaned with `git clean -fdx`, which takes `bin/`, `obj/` and `.claude/scratch/` with it. The runner refuses any directory not marked `chain-next.dedicated`. Before that, the runner's clone of the gaffer plugin is fast-forwarded to gaffer's `main` and its commit logged, so the chain's stages come from the gaffer `main` holds now; that update is never destructive — a plugin clone on another branch, or one with local commits, stops the firing before an issue is selected, and so parks nothing.
- **Tracker.** GitHub, read through `gh`; "blocking issue" means GitHub's native issue dependencies.
- **Only issues nobody else has written on.** The repository is public, so anyone can post a comment shaped like a review, which a confirmation would fold into the body. The runner skips, and names, any issue raised by or commented on by an account other than the maintainer and the review bot. A comment counts even after it is deleted, because GitHub records the author of a comment someone else deleted. Contributors still raise issues and comment freely; the maintainer builds those issues by hand.
- **Report mode.** `scripts/chain-next.sh --dry-run`.
- **What a parked comment says.** The failed stage and its position, the reason from the chain's closing line, the log path, the kept `attempt/<N>-*` branch, any branch pushed without a pull request, and a closing line saying that removing the `parked` label builds the issue again from scratch. Where the chain exited **75** — its code for a stage the Claude usage limit cut off — that closing line says instead that nothing on the issue needs looking at, and to remove the label once the limit has reset. The runner reads the exit code and not the reason, because the chain already decided the question from the reply's typed error field, where the reason is prose a report could have quoted. The runner's own `#<N> parked` line names the limit the same way, since it carries the same reason.

**Prerequisites** on the build machine: everything `scripts/chain.sh` needs (`git`, `claude`, `gh`, `jq`, `timeout`, `claude` and `gh` signed in non-interactively, and the gaffer clone from the setup below), plus `dotnet`, `flock` (util-linux) and systemd with lingering enabled for the build user (`loginctl enable-linger`), so the timer survives logout and reboot.

**One-time setup:**

1. Clone the repository somewhere used only by the runner, with a full `git clone` rather than a worktree. Mark it as the runner's:

   ```bash
   git clone https://github.com/FrankRay78/NetPace.git ~/Repos/NetPace-runner
   git -C ~/Repos/NetPace-runner config chain-next.dedicated true
   ```

   If the build user has no global `user.name` and `user.email`, set them in this clone with `git -C ~/Repos/NetPace-runner config user.name "<name>"` and the same for `user.email`.
2. Clone the gaffer plugin beside it, for the runner alone. The runner fast-forwards this clone to gaffer's `main` before each firing and the chain loads every stage from it, so nobody edits it by hand — a local commit stops the firing rather than being lost:

   ```bash
   git clone https://github.com/FrankRay78/gaffer-claude.git ~/Repos/gaffer-runner
   ```

3. Create the `parked` label once: `gh label create parked --description "The chain runner stopped on this issue; remove to retry"`.
4. Check what the first firing would build. This only reads GitHub, so it also confirms `gh` access and that both clones are where the runner expects, and it changes nothing:

   ```bash
   bash ~/Repos/NetPace-runner/scripts/chain-next.sh --dry-run
   ```

   Set `CHAIN_NEXT_CLONE` and `CHAIN_NEXT_GAFFER` first if the clones are not at the default paths. The issue it names is built, and a real pull request opened, as soon as the timer is enabled in the next step.
5. Install the schedule. Copy the unit files:

   ```bash
   mkdir -p ~/.config/systemd/user
   cp ~/Repos/NetPace-runner/scripts/systemd/netpace-chain-next.{service,timer} ~/.config/systemd/user/
   ```

   Check four lines in the copied service: `CHAIN_NEXT_CLONE`, `CHAIN_NEXT_GAFFER`, the `ExecStart` path and `PATH`. User units start with a minimal environment, so `PATH` must name where `claude`, `dotnet`, `gh`, `jq`, `git`, `flock` and `timeout` live. The shipped values suit clones at `~/Repos/NetPace-runner` and `~/Repos/gaffer-runner` with those tools in `~/.local/bin`, `~/.dotnet` or `/usr/bin`. Then start it:

   ```bash
   systemctl --user daemon-reload
   systemctl --user enable --now netpace-chain-next.timer
   ```

**Configuration.** `CHAIN_NEXT_CLONE` is the dedicated clone (default `~/Repos/NetPace-runner`). `CHAIN_NEXT_GAFFER` is the runner's clone of the gaffer plugin (default `~/Repos/gaffer-runner`), handed to the chain as `CHAIN_GAFFER`. `CHAIN_NEXT_STATE_DIR` holds `logs/` and the lock, outside the clone so the clean cannot delete them (default `${XDG_STATE_HOME:-~/.local/state}/netpace-chain`). `CHAIN_MODEL` and `CHAIN_STAGE_TIMEOUT` pass through to the chain.

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
| Finish a parked attempt instead of rebuilding it | Retrying starts from scratch and discards the kept `attempt/<N>-<timestamp>` branch, even when only a later stage failed. To keep it, fetch it into a worktree of your own checkout under the branch name the build stage reported (`git fetch ~/Repos/NetPace-runner attempt/<N>-<timestamp>:<branch>`), rebase it onto `main`, and run each stage the chain did not finish as `claude -p "/gaffer:<stage>" --plugin-dir ~/Repos/gaffer-runner` with that stage's model and effort from the stage table and `CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS=0`, as `chain.sh` does. Leave `parked` on until the pull request is open, or the next firing rebuilds the issue alongside |

**Tests.** [`scripts/chain-next.tests.sh`](../scripts/chain-next.tests.sh) covers selection (closed, not-ready, parked, blocked and already-raised issues, and issues someone else raised or commented on), report mode, a clean start after a previous run, the plugin clone brought up to date before a firing (and a clone that cannot be fast-forwarded, is on another branch or is missing, each stopping the firing with nothing parked), a reset that rewrites the runner's own source, parking and un-parking (including a usage-limit stop, whose comment names the limit, its reset wording and the label as the only thing to remove), kept attempt branches (including one whose pushed twin was removed, and one that cannot be set aside), overlapping firings, back-to-back runs, fail-closed queries (each lookup failing on its own, the lookup of who wrote on an issue included), machine faults that park nothing and the dedicated-clone guard. It uses a stub `gh` and a stub `chain.sh` in throwaway repositories, so no model or GitHub call is made. Run it after any edit to the runner.

Manual checks, on the build machine:

- **Report against the real repository.** `scripts/chain-next.sh --dry-run` names the lowest-numbered eligible issue, matching what `gh issue list --label ready` and the issues' blockers, linked pull requests, authors and commenters show, and leaves the clone, labels and comments unchanged.
- **The installed timer.** After `enable --now`, `systemctl --user list-timers` shows the next firing, and `journalctl --user -u netpace-chain-next.service` shows `chain-next: nothing eligible.` (or a run) after it fires.

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
