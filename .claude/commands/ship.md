---
description: Human-invoked orchestrator that turns "implementation looks done" into a reviewed, test-green pull request — runs the full suite first and hard-gates on it, then reviews, commits warranted fixes, raises the PR, and captures learnings.
---

Read `CLAUDE.md` for project context before proceeding.

`/ship` composes NetPace's existing commands behind one hard gate: **the full test suite runs first, and nothing downstream happens unless it is green.** The steps below are ordered and the gate between step 1 and step 2 is structural — the review step is downstream of the test run's success, so it cannot begin unless step 1 exited green. Do not add a hook to police this ordering; the exit code *is* the gate.

`/ship` is human-invoked and supervised. Composed steps MAY prompt you (e.g. `/capture-learnings`) — running fully unattended is a non-goal.

**Stop-on-failure is global:** if any step fails — a suite run is not green, a review subagent errors, `git push` is rejected, `gh pr create` fails because the PR already exists — STOP at that step, report it to the invoker, and do not run any later step.

## Steps

0. **Preconditions (before any suite run or review).**
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP immediately and report: "Run /ship from a feature branch, not main." Do not run the suite, do not spawn reviewers.
   - Run `git log main..HEAD --oneline`. If empty, STOP immediately and report: "No commits on this branch over main — nothing to ship." Do not run the suite or spawn reviewers.
   - Capture the working-tree baseline so step 3 can tell review edits apart from pre-existing local changes: record `git status --short` and the tracked-tree content hash `git diff HEAD | git hash-object --stdin` as `PRE_HASH`. A dirty tree is allowed but note it in the final report.

   The precondition guard runs up front deliberately: `/raise-pr`'s own late branch check must not be the first line of defence, or the full suite and full review would run pointlessly first.

1. **Full test run (always).** Run `dotnet build ./src && dotnet test ./src` — always, including docs-only branches. Do not add a skip path for docs-only branches: the suite is fast, and the always-on `gh pr create` `PreToolUse` hook re-runs it at step 4 anyway, so a skip saves nothing and could let review run before a late hook-block.
   - Gate on the run's **exit code**, not on any stored marker.
   - **Not green ⇒ STOP:** report the failures to the invoker and do nothing else — no review subagents, no PR.
   - **Green ⇒ continue.**

2. **Clean-context review (synchronous — this is Review A).** Spawn independent, clean-context reviewer subagents over the branch diff (`git diff main...HEAD`), then run a `/review-slop` pass. The clean-context subagents must not see the code being written — "do not inline the review" governs the *reviewing*. The *deciding-and-fixing* legitimately happens in `/ship`'s own main loop.
   - Spawn the `pr-review-toolkit` reviewers that apply to this diff. Most **report** findings — `code-reviewer`, `silent-failure-hunter`, `pr-test-analyzer`, `type-design-analyzer`, `comment-analyzer` return findings; only `code-simplifier` edits files directly. Launch them in parallel (independent, clean-context Task subagents).
   - Run `/review-slop`, which emits a cleaned diff.
   - **`/ship`'s main loop then decides.** Take the returned findings and cleaned diff, decide which warrant a change, and apply those edits to the working tree. The subagents' returned summaries landing in this loop's context is exactly how the findings reach step 5 — do not discard them.
   - If a review subagent errors, STOP and report (stop-on-failure).

3. **Conditional re-verify + commit the fixes.** Detect whether step 2 changed tracked code by a **content diff of the whole tracked working tree**, not a `*.cs`-only `git status` filter: compute `POST_HASH = git diff HEAD | git hash-object --stdin` and compare to `PRE_HASH` from step 0. This covers **all** tracked files (`.cs`, `.csproj`, `Directory.Packages.props`, `.json`, …) and correctly ignores files that were already dirty before step 2. Never use a marker mtime.
   - **If `POST_HASH == PRE_HASH` (step 2 changed nothing):** skip both the re-run and the commit — go to step 4. (No spurious second suite run.)
   - **If `POST_HASH != PRE_HASH` (step 2 applied edits):**
     - Re-run `dotnet build ./src && dotnet test ./src`. Not green ⇒ STOP and report.
     - Once green, **commit the applied edits** with a clear message (e.g. `fix: apply /ship review findings`). This is what makes the fixes reach the pushed PR — `/raise-pr` pushes *commits*, so uncommitted working-tree edits would silently never ship. If the baseline tree was already dirty, stage only the review edits, not unrelated pre-existing local changes.

4. **Raise the PR.** Compose and open the PR via `/raise-pr`. It pushes the branch (now carrying the step-3 fix commit, if any) and requests the async `@claude` GitHub-action review. If `/raise-pr` aborts (push rejected, `gh pr create` fails because the PR already exists), STOP here — do not run step 5.

5. **Capture learnings (synchronous sources only).** Run `/capture-learnings` over the session and git. Its inputs are the conversation (which now holds step 2's returned review findings) plus git — the **synchronous** Review A. `/ship` does **not** wait for, and does **not** prompt about, the asynchronous `@claude` PR review from step 4 (Review B); behaviour is identical whether or not that GitHub action posts.
   - `/ship`'s final output MAY carry a single soft nudge — "the `@claude` PR review posts async; re-run `/capture-learnings` if it flags something notable" — a free reminder, never a gate or a wait.

## Why capture-learnings feeds from the synchronous review, not the async one

`/ship` runs two reviews:

- **Review A (step 2, synchronous, in-`/ship`):** the clean-context `pr-review-toolkit` subagents + `/review-slop`. Its findings are in-context, and the fixes derived from them are committed in git, the moment step 5 runs.
- **Review B (step 4, asynchronous, on the PR):** the `@claude` GitHub-action review `/raise-pr` requests. It posts minutes later, after `/ship` has returned.

The review → memory loop closes inside a single `/ship` run via Review A. Waiting on Review B would turn a responsive supervised flow into a minutes-long poll for marginal gain, and it does not even post for a non-`FrankRay78` invoker (the `claude.yml` job is author-gated). Review B still lands on the PR for a human to read at merge — that is its audience; it is not an input to `/capture-learnings`.

## Final report

Report to the invoker: the suite result(s), which review findings were applied and committed (if any), the PR URL, and the capture-learnings outcome. If the working tree was dirty at step 0, say so.
