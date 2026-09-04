---
description: Orchestrator that turns "implementation looks done" into a reviewed, test-green pull request — runs the full suite first and hard-gates on it, then reviews, fixes every confirmed Blocker/Important finding, and raises the PR. Runs unattended, so it can drive a loop.
---

Read `CLAUDE.md` for project context before proceeding.

## User Input

```text
$ARGUMENTS
```

Optionally a GitHub issue number naming the issue this ship should close. Empty is the normal case and is **not** a prompt — step 4 forwards whatever is here to `/raise-pr`, which otherwise infers a candidate from the branch name.

`/ship` composes NetPace's existing commands behind one hard gate: **the full test suite gates everything downstream, and no review or PR happens unless it is green.** The gate is structural — the review step is downstream of the step-1 exit code, so it cannot begin against un-verified or red code. Do not add a hook to police this ordering; the exit code *is* the gate. The one step that precedes the suite is formatting (step 1a), which is cosmetic and is itself covered by the gate that follows it.

`/ship` is designed to run to completion **without prompting**, so it can be driven by an automated loop (e.g. shipping many features back-to-back) as well as invoked directly. Reflection (`/capture-learnings`) is deliberately **not** a step: it needs human curation and batches better across many features, so it belongs at a supervised checkpoint after a batch — not inside each ship, where it would either block the loop or be auto-skipped to nothing. `/ship` likewise never waits on the async `@claude` PR review (see below).

**Stop-on-failure is global:** if any step fails — a suite run is not green, a review subagent errors, `git push` is rejected, `gh pr create` fails because the PR already exists — STOP at that step, report it to the invoker, and do not run any later step.

## Steps

0. **Preconditions (before any suite run or review).**
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP immediately and report: "Run /ship from a feature branch, not main." Do not run the suite, do not spawn reviewers.
   - Run `git log main..HEAD --oneline`. If empty, STOP immediately and report: "No commits on this branch over main — nothing to ship." Do not run the suite or spawn reviewers.
   - Require a **clean working tree**. Run `git status --porcelain`; if it is non-empty, STOP and report: "Commit or stash your changes before shipping." A clean tree is what makes step 3 simple and correct: after the review, *anything* that shows up in the tree is a review edit and nothing else, so there is no need to separate review edits from pre-existing local changes.

   The precondition guard runs up front deliberately: `/raise-pr`'s own late branch check must not be the first line of defence, or the full suite and full review would run pointlessly first.

1. **Format (1a), then the full test run (1b).**

   **1a — Format the tree.** Run:

   ```bash
   dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
   ```

   The explicit `./src/NetPace.sln` argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's solution is at `src/`, not the repo root. Omitting it fails with `Could not find a MSBuild project file or solution file`.

   - Formatting runs **once per ship**, not per commit. It is cosmetic work at a cadence that already costs minutes — see *Formatting is not verification* in [agentic-workflow.md](../../docs/agentic-workflow.md).
   - **If formatting changed files, commit them now** — `git add -A` and a `style: apply dotnet format` message — *before* running the suite. This restores the clean working tree step 0 established, which is what keeps step 3's "anything in the tree is a review edit" invariant true. Do not carry format edits forward into the review commit; they are a separate concern and belong in their own commit.
   - A **non-zero exit** from `dotnet format` is a real failure (bad workspace argument, unparseable source) ⇒ **STOP and report**. A clean run that merely rewrote files is not a failure.
   - Formatting deliberately precedes 1b so that any change it makes is verified by the suite below, rather than landing after the gate has already passed.

   **1b — Full test run (always).** Run `dotnet build ./src && dotnet test ./src` — always, including docs-only branches. Do not add a skip path for docs-only branches: the suite is fast, and the always-on `gh pr create` `PreToolUse` hook re-runs it at step 4 anyway, so a skip saves nothing and could let review run before a late hook-block.
   - Gate on the run's **exit code**, not on any stored marker.
   - **Not green ⇒ STOP:** report the failures to the invoker and do nothing else — no review subagents, no PR.
   - **Green ⇒ continue.**

2. **Clean-context review (synchronous — this is Review A).** Spawn independent, clean-context reviewer subagents over the branch diff (`git diff main...HEAD`), then run a `/review-slop` pass. The clean-context subagents must not see the code being written — "do not inline the review" governs the *reviewing*. The *deciding-and-fixing* legitimately happens in `/ship`'s own main loop.
   - Spawn the `pr-review-toolkit` reviewers that apply to this diff. Most **report** findings — `pr-review-toolkit:code-reviewer`, `pr-review-toolkit:silent-failure-hunter`, `pr-review-toolkit:pr-test-analyzer`, `pr-review-toolkit:type-design-analyzer`, `pr-review-toolkit:comment-analyzer` return findings; only `pr-review-toolkit:code-simplifier` edits files directly. The `pr-review-toolkit:` prefix is required — the bare names do not resolve. Launch them in parallel (independent, clean-context Task subagents).
   - Run `/review-slop`, which emits a cleaned diff.
   - **`/ship`'s main loop applies a severity policy.** Aggregate the returned findings and cleaned diff. For each finding, first *validate it is real* — reviewer severities are fickle, so do not act on a mislabelled or false-positive finding — then act by severity:
     - **Blocker / P1** (a correctness bug, breakage, or anything that would ship broken) — **must be resolved.** Fix it in the working tree. If a confirmed blocker genuinely cannot be fixed, **STOP and report**; `/ship` never raises a PR over a known blocker.
     - **Important / P2** — fix it **when it is within the scope of this change** (the branch's own new or edited code). If a confirmed P2 concerns *pre-existing or adjacent* code the branch did not cause, do **not** force-fix it here — that folds an unrelated mission into the branch; record it in the PR body or as a follow-up instead.
     - **Suggestion / P3** — discretionary: apply if cheap and clearly correct, otherwise skip.
   Apply the warranted edits to the working tree. Every applied edit is re-verified by step 3's suite re-run, so a bad fix cannot ship green-unchecked.
   - If a review subagent errors, STOP and report (stop-on-failure).

3. **Conditional re-verify + commit the fixes.** Because step 0 required a clean tree, `git status --porcelain` now shows exactly what step 2 changed — from *any* source (the loop's own fixes **and** any files `pr-review-toolkit:code-simplifier` edited directly) — and nothing else.
   - **If `git status --porcelain` is empty (step 2 changed nothing):** skip both the re-run and the commit — go to step 4. (No spurious second suite run.)
   - **If it is non-empty (step 2 applied edits):**
     - Re-run `dotnet build ./src && dotnet test ./src`. Not green ⇒ STOP and report.
     - Once green, **commit the edits** with `git add -A` and a clear message (e.g. `fix: apply /ship review findings`). This is what makes the fixes reach the pushed PR — `/raise-pr` pushes *commits*, so uncommitted or untracked working-tree edits would silently never ship. `git add -A` is correct and complete here precisely because the tree started clean: it stages every review edit (including new untracked files and deletions) with no risk of sweeping in unrelated local changes.

4. **Raise the PR (final step).** Compose and open the PR via `/raise-pr`, forwarding any issue number from the User Input above — that pass-through is the only route by which an explicit override reaches the PR body on this path, since `/raise-pr` is never invoked directly here. With nothing supplied it infers a candidate from the branch name and verifies it, which covers the normal `/build` case. It pushes the branch (now carrying the step-3 fix commit, if any) and requests the async `@claude` GitHub-action review. If `/raise-pr` aborts (push rejected, `gh pr create` fails because the PR already exists), STOP here and report.

   `/ship` ends at the raised PR. It does **not** run `/capture-learnings`, and does **not** wait on or prompt about the async `@claude` review (Review B, see below). Its closing output carries one soft nudge — "PR raised; the `@claude` review posts async — run `/capture-learnings` when you next review the batch" — a free reminder, never a gate or a wait.

## The two reviews (Review A vs Review B)

- **Review A** — step 2, synchronous, in-`/ship`: the clean-context `pr-review-toolkit` subagents + `/review-slop`. Its findings drive the step-2 fixes and are committed in step 3 — that is how the review shapes the PR before it is raised.
- **Review B** — step 4, asynchronous, on the PR: the `@claude` GitHub-action review `/raise-pr` requests, posting minutes after `/ship` has returned. `/ship` never waits on it — it is author-gated (`claude.yml`) so it doesn't even post for a non-`FrankRay78` invoker, and blocking a pipeline for minutes to fold it in buys little. Review B is for a human to read at merge; when you later run `/capture-learnings` at a supervised checkpoint, that command's own best-effort PR-review fetch picks it up then.

## Final report

Report to the invoker: whether formatting changed anything (and the commit if it did), the suite result(s), which review findings were fixed-and-committed and which were deferred as out-of-scope follow-ups (if any), the PR URL, what `/raise-pr` settled for the closing keyword (linked, no issue, or unverified — an unverified lookup is fixable in seconds and worth surfacing, not swallowing), and the soft `/capture-learnings` nudge.
