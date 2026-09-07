---
description: Orchestrator that turns "implementation looks done" into a reviewed, test-green branch — runs the full suite first and hard-gates on it, then reviews, fixes every confirmed Blocker/Important finding, and commits. Stops short of the pull request. Runs unattended, so it can drive a loop.
---

Read `CLAUDE.md` for project context before proceeding.

`/verify` is the stage between `/build` and `/raise-pr`: it turns a feature branch into a **formatted, test-green, reviewed, fully-committed** branch with a clean working tree. It does not push, open a PR, or merge — `/raise-pr` owns all of that, and you run it yourself afterwards.

`/verify` composes NetPace's existing commands behind one hard gate: **the full test suite gates everything downstream, and no review happens unless it is green.** The gate is structural — the review step is downstream of the step-1 exit code, so it cannot begin against un-verified or red code. Do not add a hook to police this ordering; the exit code *is* the gate. The one step that precedes the suite is formatting (step 1a), which is cosmetic and is itself covered by the gate that follows it.

`/verify` is designed to run to completion **without prompting**, so it can be driven by an automated loop (e.g. verifying many features back-to-back) as well as invoked directly. Reflection (`/capture-learnings`) is deliberately **not** a step: it needs human curation and batches better across many features, so it belongs at a supervised checkpoint after a batch — not inside each verify, where it would either block the loop or be auto-skipped to nothing.

Raising the pull request is deliberately **not** a step either. It is the one irreversible, outward-facing act in the chain: everything `/verify` does is safe to re-run, and pushing a branch and opening a PR is not. Keeping it a separate deliberate `/raise-pr` invocation is what makes the rest of the chain freely repeatable.

**Stop-on-failure is global:** if any step fails — the tree is dirty, a suite run is not green, a review subagent errors — STOP at that step, report it to the invoker, and do not run any later step. Never report a branch verified over a red suite, an unresolved confirmed blocker, or an uncommitted review edit.

## Steps

0. **Preconditions (before any suite run or review).**
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP immediately and report: "Run /verify from a feature branch, not main." Do not run the suite, do not spawn reviewers.
   - Run `git log main..HEAD --oneline`. If empty, STOP immediately and report: "No commits on this branch over main — nothing to verify." Do not run the suite or spawn reviewers.
   - Require a **clean working tree**. Run `git status --porcelain`; if it is non-empty, STOP and report: "Commit or stash your changes before verifying." A clean tree is what makes step 3 simple and correct: after the review, *anything* that shows up in the tree is a review edit and nothing else, so there is no need to separate review edits from pre-existing local changes.

   The guard runs up front on cost grounds: a full format, a full suite run and a full review are minutes of work, and on `main`, on an empty branch, or over a dirty tree every one of those minutes is spent to reach a conclusion the three cheap `git` calls above already had.

1. **Format (1a), then the full test run (1b).**

   **1a — Format the tree.** Run:

   ```bash
   dotnet format style ./src/NetPace.sln && dotnet format whitespace ./src/NetPace.sln
   ```

   The explicit `./src/NetPace.sln` argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's solution is at `src/`, not the repo root. Omitting it fails with `Could not find a MSBuild project file or solution file`.

   - Formatting runs **once per verify**, not per commit. It is cosmetic work at a cadence that already costs minutes — see *Formatting is not verification* in [agentic-workflow.md](../../docs/agentic-workflow.md).
   - **If formatting changed files, commit them now** — `git add -A` and a `style: apply dotnet format` message — *before* running the suite. This restores the clean working tree step 0 established, which is what keeps step 3's "anything in the tree is a review edit" invariant true. Do not carry format edits forward into the review commit; they are a separate concern and belong in their own commit.
   - A **non-zero exit** from `dotnet format` is a real failure (bad workspace argument, unparseable source) ⇒ **STOP and report**. A clean run that merely rewrote files is not a failure.
   - Formatting deliberately precedes 1b so that any change it makes is verified by the suite below, rather than landing after the gate has already passed.

   **1b — Full test run (always).** Run `dotnet build ./src && dotnet test ./src` — always, including docs-only branches. Do not add a skip path for docs-only branches: the suite is fast, and this run is the chain's **only unconditional whole-suite gate**. The `gh pr create` `PreToolUse` hook re-runs the suite, but that hook now fires inside a separately invoked `/raise-pr` that may not happen for a long while, or at all — so skipping here would leave a branch reported verified that no suite ever ran against.
   - Gate on the run's **exit code**, not on any stored marker.
   - **Not green ⇒ STOP:** report the failures to the invoker and do nothing else — no review subagents.
   - **Green ⇒ continue.**

2. **Clean-context review (synchronous — this is Review A).** Spawn independent, clean-context reviewer subagents over the branch diff (`git diff main...HEAD`), then run a `/review-slop` pass. The clean-context subagents must not see the code being written — "do not inline the review" governs the *reviewing*. The *deciding-and-fixing* legitimately happens in `/verify`'s own main loop.
   - Spawn the `pr-review-toolkit` reviewers that apply to this diff. Most **report** findings — `pr-review-toolkit:code-reviewer`, `pr-review-toolkit:silent-failure-hunter`, `pr-review-toolkit:pr-test-analyzer`, `pr-review-toolkit:type-design-analyzer`, `pr-review-toolkit:comment-analyzer` return findings; only `pr-review-toolkit:code-simplifier` edits files directly. The `pr-review-toolkit:` prefix is required — the bare names do not resolve. Launch them in parallel (independent, clean-context Task subagents).
   - Run `/review-slop`, which emits a cleaned diff.
   - **`/verify`'s main loop applies a severity policy.** Aggregate the returned findings and cleaned diff. For each finding, first *validate it is real* — reviewer severities are fickle, so do not act on a mislabelled or false-positive finding — then act by severity:
     - **Blocker / P1** (a correctness bug, breakage, or anything that would ship broken) — **must be resolved.** Fix it in the working tree. If a confirmed blocker genuinely cannot be fixed, **STOP and report**; `/verify` never reports a branch verified over a known blocker.
     - **Important / P2** — fix it **when it is within the scope of this change** (the branch's own new or edited code). If a confirmed P2 concerns *pre-existing or adjacent* code the branch did not cause, do **not** force-fix it here — that folds an unrelated mission into the branch; name it in the final report as a deferred out-of-scope finding, or raise it as a follow-up issue.
     - **Suggestion / P3** — discretionary: apply if cheap and clearly correct, otherwise skip.
   Apply the warranted edits to the working tree. Every applied edit is re-verified by step 3's suite re-run, so a bad fix cannot reach a PR green-unchecked.
   - If a review subagent errors, STOP and report (stop-on-failure).

3. **Conditional re-verify + commit the fixes.** Because step 0 required a clean tree, `git status --porcelain` now shows exactly what step 2 changed — from *any* source (the loop's own fixes **and** any files `pr-review-toolkit:code-simplifier` edited directly) — and nothing else.
   - **If `git status --porcelain` is empty (step 2 changed nothing):** skip both the re-run and the commit — go to step 4. (No spurious second suite run.)
   - **If it is non-empty (step 2 applied edits):**
     - Re-run `dotnet build ./src && dotnet test ./src`. Not green ⇒ STOP and report.
     - Once green, **commit the edits** with `git add -A` and a clear message (e.g. `fix: apply /verify review findings`). This is what makes the fixes reach the eventual PR — `/raise-pr` pushes *commits*, so uncommitted or untracked working-tree edits would silently never leave this machine. That matters more under the split than it did before: the gap between the fix and the push is now open-ended, so an uncommitted edit has an unbounded window in which to be lost. `git add -A` is correct and complete here precisely because the tree started clean: it stages every review edit (including new untracked files and deletions) with no risk of sweeping in unrelated local changes.

4. **Stop here.** Do **not** push, open a PR, merge, or run `/raise-pr`. Leave the working tree clean — everything committed to the branch — because that is exactly `/raise-pr`'s entry condition, so the two compose by hand with nothing in between.

## The two reviews (Review A vs Review B)

- **Review A** — step 2, synchronous, in-`/verify`: the clean-context `pr-review-toolkit` subagents + `/review-slop`. Its findings drive the step-2 fixes and are committed in step 3 — that is how the review shapes the branch before a PR is ever raised.
- **Review B** — asynchronous, on the PR: the `@claude` GitHub-action review that `/raise-pr` requests when a human runs it. It is therefore outside `/verify` entirely, and nothing here waits on it. It is author-gated (`claude.yml`) so it doesn't even post for a non-`FrankRay78` invoker. Review B is for a human to read at merge; when you later run `/capture-learnings` at a supervised checkpoint, that command's own best-effort PR-review fetch picks it up then.

## Final report

Report to the invoker:

- **Verdict** — either `VERIFIED branch=<branch>`, or `FAILED reason=<short reason>` naming the precondition or gate that stopped the run. Never report verified over a red suite, an unresolved confirmed blocker, or an uncommitted change.
- Whether formatting changed anything, and the commit if it did.
- The suite result(s).
- Which review findings were fixed-and-committed, and — **named explicitly** — any confirmed finding deferred as an out-of-scope follow-up. This report is the only route by which a deferred finding reaches the PR body, because `/raise-pr` no longer runs in the same session; an unnamed deferral is a lost one.
- On a `VERIFIED` verdict, follow with: "Run `/raise-pr` to push the branch and open the PR — it derives `Closes #<N>` from this branch name and verifies it before use, then reports what it settled; check that line to confirm the link was made. The `@claude` Review B posts async on the raised PR, and `/capture-learnings` folds it in when you next review the batch."
