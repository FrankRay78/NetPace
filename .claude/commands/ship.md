---
description: Orchestrator that turns "implementation looks done" into a reviewed, test-green pull request — runs the full suite first and hard-gates on it, then reviews, fixes every confirmed Blocker/Important finding, and raises the PR. Runs unattended, so it can drive a loop.
---

Read `CLAUDE.md` for project context before proceeding.

`/ship` composes NetPace's existing commands behind one hard gate: **the full test suite runs first, and nothing downstream happens unless it is green.** The gate is structural — the review step is downstream of the step-1 exit code, so it cannot begin against un-verified or red code. Do not add a hook to police this ordering; the exit code *is* the gate.

`/ship` is designed to run to completion **without prompting**, so it can be driven by an automated loop (e.g. shipping many features back-to-back) as well as invoked directly. Reflection (`/capture-learnings`) is deliberately **not** a step: it needs human curation and batches better across many features, so it belongs at a supervised checkpoint after a batch — not inside each ship, where it would either block the loop or be auto-skipped to nothing. `/ship` likewise never waits on the async `@claude` PR review (see below).

**Stop-on-failure is global:** if any step fails — a suite run is not green, a review subagent errors, `git push` is rejected, `gh pr create` fails because the PR already exists — STOP at that step, report it to the invoker, and do not run any later step.

## Steps

0. **Preconditions (before any suite run or review).**
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP immediately and report: "Run /ship from a feature branch, not main." Do not run the suite, do not spawn reviewers.
   - Run `git log main..HEAD --oneline`. If empty, STOP immediately and report: "No commits on this branch over main — nothing to ship." Do not run the suite or spawn reviewers.
   - Capture the working-tree baseline so step 3 can detect what the review changed: snapshot the **whole** working tree — tracked content *and* untracked files — as `PRE_HASH = { git diff HEAD; git status --short; } | git hash-object --stdin`. `git diff HEAD` captures tracked content (incl. staged, and further edits to an already-dirty file); `git status --short` adds the presence of new untracked files, which `git diff HEAD` alone omits. A dirty tree is allowed but note it in the final report.

   The precondition guard runs up front deliberately: `/raise-pr`'s own late branch check must not be the first line of defence, or the full suite and full review would run pointlessly first.

1. **Full test run (always).** Run `dotnet build ./src && dotnet test ./src` — always, including docs-only branches. Do not add a skip path for docs-only branches: the suite is fast, and the always-on `gh pr create` `PreToolUse` hook re-runs it at step 4 anyway, so a skip saves nothing and could let review run before a late hook-block.
   - Gate on the run's **exit code**, not on any stored marker.
   - **Not green ⇒ STOP:** report the failures to the invoker and do nothing else — no review subagents, no PR.
   - **Green ⇒ continue.**

2. **Clean-context review (synchronous — this is Review A).** Spawn independent, clean-context reviewer subagents over the branch diff (`git diff main...HEAD`), then run a `/review-slop` pass. The clean-context subagents must not see the code being written — "do not inline the review" governs the *reviewing*. The *deciding-and-fixing* legitimately happens in `/ship`'s own main loop.
   - Spawn the `pr-review-toolkit` reviewers that apply to this diff. Most **report** findings — `code-reviewer`, `silent-failure-hunter`, `pr-test-analyzer`, `type-design-analyzer`, `comment-analyzer` return findings; only `code-simplifier` edits files directly. Launch them in parallel (independent, clean-context Task subagents).
   - Run `/review-slop`, which emits a cleaned diff.
   - **`/ship`'s main loop applies a severity policy.** Aggregate the returned findings and cleaned diff. For each finding, first *validate it is real* — reviewer severities are fickle, so do not act on a mislabelled or false-positive finding — then act by severity:
     - **Blocker / P1** (a correctness bug, breakage, or anything that would ship broken) — **must be resolved.** Fix it in the working tree. If a confirmed blocker genuinely cannot be fixed, **STOP and report**; `/ship` never raises a PR over a known blocker.
     - **Important / P2** — fix it **when it is within the scope of this change** (the branch's own new or edited code). If a confirmed P2 concerns *pre-existing or adjacent* code the branch did not cause, do **not** force-fix it here — that folds an unrelated mission into the branch; record it in the PR body or as a follow-up instead.
     - **Suggestion / P3** — discretionary: apply if cheap and clearly correct, otherwise skip.
   Apply the warranted edits to the working tree. Every applied edit is re-verified by step 3's suite re-run, so a bad fix cannot ship green-unchecked.
   - If a review subagent errors, STOP and report (stop-on-failure).

3. **Conditional re-verify + commit the fixes.** Detect whether step 2 changed anything by re-computing the step-0 whole-tree snapshot — `POST_HASH = { git diff HEAD; git status --short; } | git hash-object --stdin` — and comparing to `PRE_HASH`. Hashing `git diff HEAD` covers tracked content across **all** file types (`.cs`, `.csproj`, `Directory.Packages.props`, `.json`, …) and distinguishes a further edit to an already-dirty file; adding `git status --short` covers **new untracked files** a review may create (a new test, an extracted helper) — which `git diff HEAD` alone silently omits, and missing one means the fix never reaches the PR. Never a `*.cs`-only filter, never a marker mtime.
   - **If `POST_HASH == PRE_HASH` (step 2 changed nothing):** skip both the re-run and the commit — go to step 4. (No spurious second suite run.)
   - **If `POST_HASH != PRE_HASH` (step 2 applied edits):**
     - Re-run `dotnet build ./src && dotnet test ./src`. Not green ⇒ STOP and report.
     - Once green, **commit the applied edits** with a clear message (e.g. `fix: apply /ship review findings`). This is what makes the fixes reach the pushed PR — `/raise-pr` pushes *commits*, so uncommitted or untracked working-tree edits would silently never ship. Stage **the specific paths step 2's loop edited or created** (`git add <those paths>` — the loop knows them, and this includes any new files), not `git add -A`; on an already-dirty baseline this keeps unrelated pre-existing changes out of the commit without needing to isolate hunks.

4. **Raise the PR (final step).** Compose and open the PR via `/raise-pr`. It pushes the branch (now carrying the step-3 fix commit, if any) and requests the async `@claude` GitHub-action review. If `/raise-pr` aborts (push rejected, `gh pr create` fails because the PR already exists), STOP here and report.

   `/ship` ends at the raised PR. It does **not** run `/capture-learnings`, and does **not** wait on or prompt about the async `@claude` review (Review B, see below). Its closing output carries one soft nudge — "PR raised; the `@claude` review posts async — run `/capture-learnings` when you next review the batch" — a free reminder, never a gate or a wait.

## The two reviews (Review A vs Review B)

- **Review A** — step 2, synchronous, in-`/ship`: the clean-context `pr-review-toolkit` subagents + `/review-slop`. Its findings drive the step-2 fixes and are committed in step 3 — that is how the review shapes the PR before it is raised.
- **Review B** — step 4, asynchronous, on the PR: the `@claude` GitHub-action review `/raise-pr` requests, posting minutes after `/ship` has returned. `/ship` never waits on it — it is author-gated (`claude.yml`) so it doesn't even post for a non-`FrankRay78` invoker, and blocking a pipeline for minutes to fold it in buys little. Review B is for a human to read at merge; when you later run `/capture-learnings` at a supervised checkpoint, that command's own best-effort PR-review fetch picks it up then.

## Final report

Report to the invoker: the suite result(s), which review findings were fixed-and-committed and which were deferred as out-of-scope follow-ups (if any), the PR URL, and the soft `/capture-learnings` nudge. If the working tree was dirty at step 0, say so.
