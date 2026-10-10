---
description: Orchestrator that turns "implementation looks done" into a reviewed, test-green branch — runs the full suite first and hard-gates on it, then reviews the branch once, fixes the confirmed findings its rules table calls for, and commits them on a green re-run. Stops short of the pull request. Runs unattended, so it can drive a loop.
---

Read `CLAUDE.md` for project context before proceeding.

`/verify` is the stage between `/build` and `/raise-pr`: it turns a feature branch into a **formatted, test-green, reviewed, fully-committed** branch with a clean working tree. It does not push, open a PR, or merge — `/raise-pr` owns all of that, and you run it yourself afterwards.

`/verify` composes NetPace's existing commands behind one hard gate: **the full test suite gates everything downstream, and no review happens unless it is green.** The gate is structural — the review step is downstream of the step-1 exit code, so it cannot begin against un-verified or red code. Do not add a hook to police this ordering; the exit code *is* the gate. The one step that precedes the suite is formatting (step 1a), which is cosmetic and is itself covered by the gate that follows it.

**The review is one round, and its fix is read on the pull request.** A fix written in response to a review is new code, and someone who did not write it should read it. That reader is Review B — the `@claude` review `/raise-pr` requests, which reads the whole PR diff, fix commit included — not a further round here. Re-reviewing the fix inside `/verify` was tried and fed itself (step 2 says how), so step 2 runs once and step 3 commits what it led to.

`/verify` is designed to run to completion **without prompting**, so it can be driven by an automated loop (e.g. verifying many features back-to-back) as well as invoked directly. Reflection (`/capture-learnings`) is deliberately **not** a step: it needs human curation and batches better across many features, so it belongs at a supervised checkpoint after a batch — not inside each verify, where it would either block the loop or be auto-skipped to nothing.

Raising the pull request is deliberately **not** a step either. It is the one irreversible, outward-facing act in the chain: everything `/verify` does is safe to re-run, and pushing a branch and opening a PR is not. Keeping it a separate deliberate `/raise-pr` invocation is what makes the rest of the chain freely repeatable.

**Stop-on-failure is global:** if any step fails — the tree is dirty, a suite run is not green, the format tool errors, a review subagent errors, a confirmed finding that must be fixed cannot be — STOP at that step, report it to the invoker, and do not run any later step. Every such report says what state the working tree is in: clean, or holding uncommitted edits, and whose they are.

## Steps

**Every STOP below ends its report with the verdict line, and nothing after it.** Where a step names the reason (`FAILED reason=dotnet format failed`), report that reason verbatim; where it does not, write a short reason of your own. Whatever else the stop has to say — which finding was raised and by whom, what state the tree is in, what the recovery is — goes *above* that line, however naturally it reads to close on it. This is stated once here because it governs all of them: a stop message whose last line is not the verdict reaches `scripts/chain.sh` as the far less useful `no readable verdict` instead of the reason, and `scripts/chain-next.sh` then parks the issue with that non-reason — losing a two-hour stage's diagnosis to a formatting slip.

0. **Preconditions (before any suite run or review).** Each of the three is a **failure**, so each STOPS with the *Final report*'s `FAILED reason=` verdict line as the report's last line and nothing after it. Report the reason below verbatim; the explanatory sentence for a human goes *above* the verdict line.
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP immediately with `FAILED reason=/verify was run on main, not a feature branch`. Do not run the format pass or the suite, do not spawn reviewers.
   - Run `git log main..HEAD --oneline`. If empty, STOP immediately with `FAILED reason=no commits on this branch over main`. Do not run the suite or spawn reviewers.
   - Require a **clean working tree**. Run `git status --porcelain`; if it is non-empty, STOP with `FAILED reason=the working tree has uncommitted changes`, adding that committing or stashing them is the recovery. A clean tree is what makes step 3 simple and correct: after the review, *anything* that shows up in the tree is a review edit and nothing else, so there is no need to separate review edits from pre-existing local changes.

   The first two checks run up front purely on cost grounds: a full format, a full suite run and a full review are minutes of work, and on `main` or an empty branch every one of those minutes is spent to reach a conclusion a cheap `git` call already had. The clean-tree check earns its place differently — it is the invariant that makes step 3 correct, as described above.

1. **Format (1a), then the full test run (1b).**

   **1a — Format the tree.** Run:

   ```bash
   dotnet format ./src/NetPace.sln
   ```

   **This is the bare command, deliberately — the same one CI runs** as `dotnet format ./src/NetPace.sln --verify-no-changes` ([dotnet.yml](../../.github/workflows/dotnet.yml)). It was previously the narrower `dotnet format style … && dotnet format whitespace …`, which can leave a tree that CI's check then rejects: a branch verified here would fail the format check on the pull request. Use CI's command, so what passes here is what passes there, whatever the gap between them turns out to be. The bare command also runs analyzer fixes, so read what it changed rather than assuming it only moved whitespace.

   The explicit `./src/NetPace.sln` argument is **required, not decorative**: `dotnet format` looks for a project or solution in the *current directory only*, and NetPace's solution is at `src/`, not the repo root. Omitting it fails with `Could not find a MSBuild project file or solution file`.

   - Formatting runs **once here, and once more in step 3 if the review led to edits** — never per commit. It is cosmetic work at a cadence that already costs minutes — see *Formatting is not verification* in [agentic-workflow.md](../../docs/agentic-workflow.md). This pass exists to bring the branch as `/build` left it into line; step 3's exists because a review fix written after this pass would otherwise reach the pull request unformatted.
   - **If formatting changed files, commit them now** — `git add -A` and a message in the same `Refs #<N>: <imperative>` form step 3 uses, e.g. `Refs #328: apply dotnet format` — *before* running the suite. This restores the clean working tree step 0 established, which is what keeps step 3's "anything in the tree is a review edit" invariant true. Do not carry format edits forward into the review commit; they are a separate concern and belong in their own commit.
   - **If formatting changed nothing, make no commit.** A branch already in format reaches step 2 with the commits `/build` left and no format commit.
   - A **non-zero exit** from `dotnet format` is a real failure (bad workspace argument, unparseable source) ⇒ **STOP and report** with `FAILED reason=dotnet format failed`. A clean run that merely rewrote files is not a failure — that is the tool doing its job, and the distinction is the exit code, not whether `git status` is dirty afterwards.
   - Formatting deliberately precedes 1b so that any change it makes is verified by the suite below, rather than landing after the gate has already passed.

   **1b — Full test run (always).** Run `dotnet build ./src && dotnet test ./src` — always, including docs-only branches. Do not add a skip path for docs-only branches: the suite is fast, and this is the first whole-suite gate after `/build`, whose run precedes formatting and review fixes. The `gh pr create` `PreToolUse` hook re-runs the suite, but that hook fires inside a separately invoked `/raise-pr` that may not happen for a long while, or at all — so skipping here would leave a branch reported verified that no suite ever ran against.
   - Gate on the run's **exit code**, not on any stored marker.
   - **Not green ⇒ STOP:** report the failures to the invoker and do nothing else — no review subagents.
   - **Green ⇒ continue.**

2. **Review (synchronous — this is Review A).** One round, over the whole branch diff, `git diff main...HEAD`. Note `git rev-parse HEAD` before spawning anyone; step 3 opens by checking it.

   **There is no second round.** `/verify` used to review its own fix commit, and then the fix to that, for up to four rounds. The later rounds fed themselves: every one reviewed only text `/verify` had just written, the fix commits did not shrink from round to round, and most runs never reached a round that found nothing (#362 has the figures). What reads the fix commit now is Review B on the pull request, which reads the whole PR diff. So do not go back over your own fixes with another set of reviewers, however short the fix.

   **Who reviews.** Spawn independent, clean-context reviewer subagents over the branch diff and run a `/review-slop` pass, in the two waves below. The clean-context subagents must not see the code being written — "do not inline the review" governs the *reviewing*. The *deciding-and-fixing* legitimately happens in `/verify`'s own main loop.
   - Spawn the `pr-review-toolkit` reviewers that apply to this diff, as independent, clean-context Task subagents. The `pr-review-toolkit:` prefix is required — the bare names do not resolve. Leave out any reviewer that does not apply (e.g. `pr-review-toolkit:type-design-analyzer` on a docs-only diff). They run in **two waves**:
     - **Wave 1 — the reporters, in parallel.** `pr-review-toolkit:code-reviewer`, `pr-review-toolkit:silent-failure-hunter`, `pr-review-toolkit:pr-test-analyzer`, `pr-review-toolkit:type-design-analyzer` and `pr-review-toolkit:comment-analyzer` only **report** findings. Launch them together, and run `/review-slop` (which emits a cleaned diff) while they work.
     - **Wave 2 — `pr-review-toolkit:code-simplifier`, alone, only after every wave-1 reviewer you launched and `/review-slop` have returned.** It is the one reviewer that **edits files directly**, so it must not run while anything is still reading the tree. If it does not apply to the diff, skip wave 2. While it runs you may aggregate and validate wave-1 findings, but make no edit to the working tree until it has returned. Wave-1 findings and the cleaned diff are anchored to HEAD: before applying one, relocate it in the current file, and drop any the simplifier's edits already resolved.
   - **Keep the waves — never run `code-simplifier` alongside the reporters.** They share one working tree, so a concurrent simplifier rewrites files the reporters are still reading, and their `file:line` anchors silently drift between the edited file and HEAD (observed in #265: reporters on the same finding cited `:27-28` and `:30-31`). Do not fix this instead by giving the simplifier its own worktree: step 3 finds review edits through `git status --porcelain`, and edits made in another worktree never show up there, so they would never be committed.

   **Ask every reviewer for a severity on one scale.** Tell each one to rate every finding it reports **Blocker**, **Important** or **Suggestion**, and to say which. The reviewers' own prompts rate on scales of their own and cannot be edited, so the brief is the only place this scale can be asked for.

   **What to do with the findings.** Aggregate the returned findings and the cleaned diff. For each finding, first *validate it is real* — do not act on a false positive. Then find its row in the table below by severity and scope. **The table and the definitions under it are the only statement of which outcome a finding gets.**

   | Finding | Outcome |
   |---|---|
   | Rejected as not real | Not acted on |
   | **Blocker** or **Important**, in scope | **Fix it** |
   | **Blocker** or **Important**, out of scope | Not fixed |
   | **Suggestion**, any scope | Discretionary |

   What the table's words mean:

   - **Severity** — the highest rating any reviewer who reported the finding gave it, on the scale they were briefed to use. A reviewer who reported it with no rating on that scale — unrated, or rated on a scale of its own — counts as having rated it Important when the finding is a defect and Suggestion when it is not, and the report marks the finding **unrated**. A hunk of the cleaned diff is a Suggestion. One more thing raises a severity, and nothing lowers it: a confirmed defect in scope takes at least the Important row whatever its reviewer called it. A *defect* is something wrong — code that misbehaves or breaks something, text that states something false — as against an improvement to something already correct.
   - **Blocker** — a correctness bug, breakage, or anything that would ship broken.
   - **In scope** — the finding concerns the branch's own new or edited code, or breakage that code causes elsewhere. **Out of scope** — it concerns code the branch did not touch, *and* you have checked that the problem is there on `main`, unchanged by this branch. A finding you have not shown that for is in scope, wherever the file is.
   - **Fix it** — fix it in the working tree, with the smallest change that removes the defect (see below). If a confirmed one genuinely cannot be fixed, **STOP and report**, naming the finding, the edits already applied for other findings, and that those edits are uncommitted and unverified. Discarding them is the recovery: committing them to satisfy step 0 on a later run would put untested code on the branch. Declining to fix one and leaving the tree empty is not a clean review.
   - **Not fixed** — do not fix it here: that folds an unrelated mission into the branch. Name it in the final report with its severity. It does not block the verdict.
   - **Discretionary** — apply it if cheap and clearly correct, otherwise skip it.
   - **Not acted on** — nothing is changed for it, and it does not block the verdict.

   **Make the smallest change that removes the defect.** A fix is the only code on the branch no reviewer inside `/verify` reads, so keep it small enough to be read at a glance on the pull request. Where a sentence states something false, delete it or correct the one fact — do not write a fuller account in its place. Where code misbehaves, change the lines that misbehave — do not restructure around them. Add a test, a guard, a comment or a document only where the finding itself is that it is missing. Anything larger that the review made you want belongs in its own issue, not in this commit.

   **A rating can be raised, never lowered.** You may reject a finding as *not real*: that is a judgement about the facts. You may **not** re-rate a real Blocker or Important down to a Suggestion, nor decide a real one is "minor enough", nor narrow "the branch's own code" to exclude a file the branch edited, nor call a finding out of scope without the check that definition requires. The thing deciding the verdict must not be the thing that wants the run to finish.

   If a review subagent errors, STOP and report (stop-on-failure) — a wave-1 error means wave 2 is never launched.

3. **Re-verify and commit the review's edits.** Step 0 required a clean tree and step 1a committed its own formatting, so `git status --porcelain` now shows exactly what the review changed — from *any* source (your own fixes **and** any files `pr-review-toolkit:code-simplifier` edited directly) — and nothing else.
   - **If the tree is clean and HEAD is where step 2 noted it:** the review changed nothing and the branch is verified. Skip the format pass, the re-run and the commit — go to step 4. (No spurious second suite run.) The last suite run was against exactly the committed code.
   - **Otherwise** — the tree has edits, or HEAD has moved because a reviewer committed something itself:
     - **Format, before the suite re-run** — `dotnet format ./src/NetPace.sln`, the same bare command as step 1a and the same one CI checks. A review fix is written fast and is routinely off-format, and this is the only pass that ever sees it: step 1a ran before the fix existed. A non-zero exit is a real failure ⇒ **STOP and report** `FAILED reason=dotnet format failed`; the tool merely rewriting files is not.
     - Re-run `dotnet build ./src && dotnet test ./src`. Not green ⇒ STOP and report — and say that the tree is dirty, that the edits in it are the review's failed fix (and its formatting), and that discarding them is the recovery. Do **not** commit them to satisfy step 0's clean-tree precondition on a later run: that would put a red fix on the branch, which is the one thing this step exists to stop. If HEAD had moved, name the commits from the noted value to HEAD as well: they are a reviewer's own, and they are in the red run too.
     - Once green, **commit whatever the tree holds** with `git add -A` and a message in the repo's `Refs #<N>: <imperative>` form — e.g. `Refs #328: apply /verify review findings`, with the issue number read off the branch name, not left as a placeholder. If the branch name carries no issue number, drop the `Refs #<N>: ` prefix and keep the rest; the constitution requires it only where an issue applies. Commit immediately, with no further edit in between, so the suite run above was against exactly the code the commit contains. This is what makes the fixes reach the eventual PR — `/raise-pr` pushes *commits*, so uncommitted or untracked working-tree edits would silently never leave this machine. `git add -A` is correct and complete here precisely because the tree started clean: it stages every review edit and its formatting (including new untracked files and deletions) with no risk of sweeping in unrelated local changes. If the format pass put every edit back and the tree is clean, there is nothing to commit.
     - The branch is verified. Go to step 4 — **do not review the commit you just made.**

4. **Stop here.** Do **not** push, open a PR, merge, or run `/raise-pr`. Leave the working tree clean — everything committed to the branch — because that is exactly `/raise-pr`'s entry condition, so the two compose by hand with nothing in between.

## The two reviews (Review A vs Review B)

- **Review A** — steps 2 and 3, synchronous, in-`/verify`: the clean-context `pr-review-toolkit` subagents plus `/review-slop`, run once over the branch diff. Its findings drive the step-2 fixes, which step 3 commits — that is how the review shapes the branch before a PR is ever raised.
- **Review B** — asynchronous, on the PR: the `@claude` GitHub-action review that `/raise-pr` requests. It reads the whole PR diff, so it is what reads the commit step 3 made; it is handed none of Review A's findings, so it judges the branch on what it sees. It is therefore outside `/verify` entirely, and nothing here waits on it. It is author-gated (`claude.yml`) so it doesn't even post for a non-`FrankRay78` invoker. Review B is for a human to read at merge; when you later run `/capture-learnings` at a supervised checkpoint, that command's own best-effort PR-review fetch picks it up then.

## Final report

Report, in this order:

- Whether formatting changed anything, and the commit if it did — step 1a's, and step 3's if its commit carried formatting alongside the fix.
- The suite result(s).
- Which reviewers ran, and the commit step 3 made, if the review led to one. If HEAD had moved by step 3 because a reviewer committed itself, name each such commit by hash and say which reviewer made it: it is on the branch, but nothing in `/verify` judged it against step 2's rules.
- Which review findings were fixed-and-committed, and which confirmed ones were not — each *Not fixed* finding and each Suggestion left unapplied, by name and severity. Mark as **unrated** any finding whose severity you set because its reviewer gave none on the scale, with one line on why it was or was not counted a defect. **This report is the only place an unfixed finding is recorded, and it ends there.** Do not raise an issue for one, write it to a file, or ask for it to go in the PR body: nothing carries Review A's findings to the pull request, so that Review B reads the branch untainted by them.
- On the way to a `VERIFIED` verdict, add this **above the verdict line** — it is boilerplate this command mandates, and appending it below the verdict is the commonest way to lose the verdict: "Run `/raise-pr` to push the branch and open the PR — it derives `Closes #<N>` from this branch name and verifies it before use, then reports what it settled; check that line to confirm the link was made. The `@claude` Review B posts async on the raised PR, and `/capture-learnings` folds it in when you next review the batch."

**Close the report with the verdict on its own last line** — plain, at column 1, nothing else on that line and nothing after it. Exactly one of:

- `VERIFIED branch=<branch>` — on success.
- `FAILED reason=<short reason>` — on any failure.

Both are written bare, never inside a code fence: `scripts/chain.sh` reads the last non-blank line of the report, so a fence closing beneath the verdict is the last line and the verdict is lost. The fixed reasons this command emits are `dotnet format failed` (step 1a or step 3) and step 0's three precondition reasons. Anything else is a short reason of your own.

That one line is the whole of what the chain reads, which is what makes it safe and what makes it strict:

- **Nothing may follow it.** Not a closing sentence, not a fence, not a blank-line-and-a-postscript. A report whose last line is anything but a verdict stops the chain as unreadable — a safe stop, but a wasted run.
- **Nothing above it can be mistaken for it.** Quote either verdict as often as the report needs to — `not VERIFIED`, a `FAILED reason=` a reviewer raised, a table of both — and it changes nothing. The rule that used to forbid writing the other verdict's word anywhere in the report is gone with the prose scanning that needed it.
- **Decoration on the verdict line is tolerated, not invited.** One leading heading, bullet, numbered-item or blockquote marker — one, not a stack — and a run of `*`, `_` or backticks wrapping the line, are stripped; so is an indent of up to three spaces. `Verdict: FAILED reason=…` is not a verdict, because the line must open with the verdict word, and neither is a line carrying anything *besides* the verdict: `VERIFIED branch=x (2 Important deferred)` is read as no verdict at all, not as a pass.

Never report verified over a red suite, a confirmed finding whose row in step 2's table is **Fix it** and that is still unresolved, or an uncommitted change.
