---
description: Record what surprised a piece of work — each surprise as one classified row in docs/study/<issue>.md — so the harness can be improved from evidence. A clean run writes nothing. Never prompts.
---

Read `CLAUDE.md` for project context before proceeding.

`/study` records what *surprised* a piece of work: a mid-flight redesign, a reviewer finding that had to be actioned, an acceptance criterion that turned out ambiguous. Each surprise becomes one row — the finding, the level it sits at, and what was done about it — in `docs/study/<N>.md`.

It complements `/build` and `/verify` without being wired into either. Run it at any point; run it more than once. It changes no source, test or configuration file, runs no tests, applies no formatting, and never pushes, opens a PR, or merges. The only thing it ever writes is a study record, and it commits what it writes.

**A clean run must write nothing.** There is no "nothing notable" row. Because the folder has no filler in it, the *presence* of a file means the work taught us something, and a reader can trust every row they find. The denominator — how much shipped without incident — stays recoverable from merged-PR history. Recording nothing is the normal, expected outcome, not a failure to try hard enough.

**Asked what surprised it, a model will readily invent plausible surprises.** That is the failure mode this command exists to avoid, because a folder of confabulation is worse than no folder at all. Every row must be grounded in evidence the run can actually point to — a commit, a review comment, a failing test, a CI run. A finding you cannot ground is not recorded, however plausible it sounds.

`/study` **never prompts.** With no argument it infers the issue from the branch; if it cannot, it stops and reports. Everything it does runs unattended, so an automated chain can invoke it exactly like a human does.

**Stop-on-failure is global, and failure is inert:** if any step fails, STOP, report it, and leave the repository exactly as you found it. `/study` never blocks or fails anything else — a failure here is a missing record, not a broken build.

---

## User Input

```text
$ARGUMENTS
```

Optionally a GitHub issue number — bare (`260`), hashed (`#260`), or a full issue URL. Empty is the normal case: step 2 infers the number from the branch name. `/study` never asks the invoker for anything.

---

## The four levels

Every row is classified to **exactly one** level. The levels are not severities — they say *where the fix belongs*, which is what makes a recurring level actionable.

- **Execution** — the goal and the written criteria were right, the doing went wrong. Recurring ⇒ improve the command prompts, the gates, or the tooling.
- **Plan-spec** — the criteria were wrong, ambiguous, or incomplete. Recurring ⇒ improve how issues are drafted upstream.
- **Codebase** — the doing and the criteria were both sound; the existing code was not what anyone assumed. A hidden coupling, a landmine, debt that made the work harder than the criteria implied. Recurring ⇒ a refactor, or issues raised against the code itself.
- **Environment** — the surprise came from outside the repository: a check that failed on a clean runner after passing locally, a tool or service that was unavailable, a dependency that moved. Recurring ⇒ harden the environment, or the way the harness depends on it.

**Codebase vs Execution is the judgement call**, and it will sometimes be got wrong. The test is where the *fix* belongs: a prompt or gate change is Execution; a change to code that was already there is Codebase.

There is deliberately **no *Goal* level** — no row for "this work should not have been done at all". That judgement is settled when the issue is drafted, not downstream while building it, so the level is not available to the thing doing the work and would sit empty, diluting the tally.

---

## Steps

1. **Preconditions.**
   - Run `git rev-parse --abbrev-ref HEAD` and keep the result as `<branch>` — step 3 needs it. STOP and report if the command fails (this is not a git repository), if it returns `main` ("Run /study from a feature branch, not main."), or if it returns `HEAD`, which means a detached HEAD — a commit made there lands on no branch and is lost at the next checkout, while the report would claim the record was committed. Write no file, create no commit, create no branch.
   - Run `git status --porcelain` and keep the output as the **baseline**. `/study` deliberately does not require a clean tree, so this is the only record of what was already dirty; steps 6 and 7 compare against it, and the report names it.
   - Confirm `docs/study/README.md` exists. If it does not, STOP and report — and **never author a replacement**. It defines the four levels for every file in the folder, so an invented one would misclassify every record under it. This is the one place the command could confabulate the very document that defines its classification, which is why it refuses rather than improvises.

2. **Resolve the issue number.**
   - If the invoker gave one, normalise it — strip `#`, strip any leading zeros, take the trailing number of a URL — and use it.
   - Otherwise infer it from `<branch>`, and only from the `<prefix>/<N>-<slug>` shape `/build` produces: take the leading number off the segment after the prefix, so `feature/260-study-command` yields `260`. Two shapes yield **no candidate** and fall through to the stop below — a post-prefix segment that does not begin with digits followed by `-` (`feature/net10-upgrade` must not yield `10`), and a bare `NNN-<slug>` spec-kit branch (those are *sequence* numbers restarting from `001`, not issue numbers). Never hunt for a number elsewhere in the name. This mirrors `/raise-pr`'s inference rule deliberately.
   - If there is no argument and `<branch>` yields no candidate, STOP and report that the issue could not be inferred. Write nothing, commit nothing. Guessing an issue number files the record against the wrong work, and unlike `/raise-pr` there is no later `gh` check that would catch it.

3. **Gather the evidence, and classify each source.** Take whatever is available at the moment you run, and classify each source below as exactly one of **had** (you gathered it), **absent** (it legitimately does not exist), or **errored** (the command to gather it failed).

   **Never fold `errored` into `absent`.** That distinction is the difference between "nothing was surprising" and "I could not look", and collapsing it is what would let a run that gathered nothing report identically to a clean one. Every source here is best-effort: an `absent` or `errored` source is named in the final report and never stops the run by itself. Note the classification **for the report only** — the file never carries an evidence-sources line.

   - **The live conversation** — `had` only when `/study` runs in the same session as the work. It is the richest source and the only one holding a mid-flight redesign that left no commit behind. Run from a fresh session it is `absent`, and findings that existed only there are already lost.
   - **The branch's commits** — `git log main..HEAD --oneline`. Revert pairs, a commit a later commit undoes, and a file rewritten repeatedly are all evidence of a surprise that cost rework.
   - **The diff** — `git diff main...HEAD --stat` for shape, and the diff itself where a specific finding needs grounding. For this and the bullet above, `main` not being a local ref is `errored`, not an empty result.
   - **PR review comments** — if the branch has a PR, `gh pr view --json comments,reviews`. This is where the asynchronous `@claude` review lands. No PR, or no review posted yet, is `absent` — never wait or poll for one. A `gh` call that *fails* is `errored`, and the exit code alone will not tell the two apart: `gh pr view` exits non-zero both when no PR exists and when `gh` is unauthenticated, rate-limited or offline. Read stderr and classify on that; if you cannot tell, classify it `errored`.
   - **CI runs** — `gh run list --branch <branch>` and the logs of any failure. A check that failed on a clean runner after passing locally is the archetypal Environment row. No runs for this branch is `absent`; a failing `gh` call is `errored`.

   **If no source is `had`, STOP and report `FAILED reason=no evidence gathered`.** A run that could not look is never reported as a run that found nothing.

4. **Decide what, if anything, was genuinely surprising.** For each candidate, apply both tests before it earns a row:
   - **Was it a surprise?** Something that was not anticipated by the issue as written and by a competent reading of the code. Work proceeding as expected is not a surprise, however much of it there was. A reviewer finding that had to be actioned *is* a surprise — it is exactly the kind of routine cost the accumulated tally exists to measure, and excluding it would leave the Execution count meaningless.
   - **Can you point at the evidence?** Name the commit, the review comment, the failing test, the CI run. The Finding text carries its source inline where it has one — e.g. `Reviewer (silent-failure-hunter): …`, `CI (ubuntu-latest): …`. There is no separate evidence column; the row grounds itself in its own prose.

   If nothing passes both tests, go to step 7 with zero rows. That is the expected outcome for a clean run.

   There is **no cap** on rows. Record every finding that passes both tests.

5. **Write the record.** `docs/study/<N>.md`, one file per issue.
   - **New file** — the H1 needs the issue title, so run `gh issue view <N> --json title` now. If it cannot be fetched, STOP and report, naming which failure it was: a `404` (no such issue — the number is wrong, or was mis-inferred from the branch), an auth or rate-limit response, or no network. Those have different owners, and the reason line is the only thing the invoker sees. An invented title mislabels the record permanently. Then write:

     ```markdown
     # <N> — <issue title>

     | Finding | Level | Fix applied |
     | --- | --- | --- |
     | … | … | … |
     ```

   - **Existing file** — append new rows to the same flat table; the H1 is already there and no title fetch is needed. Do **not** add a per-pass section, a date, or a pass identifier: a reader tallying levels never needs to know which pass wrote a row, and per-pass headings would fragment the table they are tallying.
   - **Dedup is a judgement call against the existing row text.** Read the rows already there; a finding already recorded is not recorded again, even if this run reached it by a different route. Leave every existing row exactly as it is — `/study` appends, it never edits or removes. **If every candidate is already present, nothing is written — go to step 7.**
   - The file contains **only** the H1 and the table. No evidence-sources line, no dates, no prose, no per-pass heading.
   - Write markdown one line per row and paragraph; no hard column wrapping.

6. **Commit what you wrote, to the current feature branch.** Only reached when step 5 actually wrote or changed the file. The commit lands on the branch you are already on — never a new branch, never `main` — so the record travels with the work's pull request rather than arriving separately.
   - Stage the study record by **explicit path** — `git add docs/study/<N>.md` — never `git add -A`, which would sweep unrelated working-tree changes into a documentation commit. If the file already carried uncommitted rows from an earlier run, they are committed along with this run's: they are study rows for this issue, step 5's dedup has already reconciled them, and stranding them would leave good findings uncommitted.
   - Commit in imperative mood referencing the issue: `Refs #<N>: record study findings`. Use `Refs #<N>`, never a closing keyword — a closing keyword would close the issue the moment the commit reached `main`.
   - **If any part of this step fails, undo the write before reporting.** A commit can be rejected for reasons that have nothing to do with `/study` — `no-skipped-tests.sh` is a `PreToolUse` hook that blocks *every* `git commit` while a banned construct exists anywhere under `src/`, and fails closed if it cannot scan; there may also be no configured `user.email`, a failing signature, or a full disk. Unstage what you staged (`git restore --staged docs/study/<N>.md`), then delete a file you created or `git restore` a file you appended to, and confirm `git status --porcelain` matches the step-1 baseline before reporting `FAILED`. Leaving the write behind would dirty the tree and block `/verify` — the one thing this command promises never to do. If you genuinely cannot restore it, say so and name the paths rather than claiming the repository is unchanged.
   - If the baseline recorded pre-existing changes to any file **other than** `docs/study/<N>.md`, leave them exactly as they were and name them in the report. They are not `/study`'s to commit. The study record itself is the one exception, per the bullet above.

7. **Nothing to record (exit path from steps 4 and 5).** Reached when step 4 found nothing, or step 5 found every candidate already present. Write no file, make no edit, create no commit. Leave the working tree exactly as the step-1 baseline found it and say so plainly in the report. Do not create an empty file, and do not add a "nothing notable" row.

8. **Stop here.** Do not push, do not open or modify a pull request, do not merge, do not run any other command.

---

## What `/study` must never touch

- **No source, test, or configuration file.** `/study` writes under `docs/study/` and nowhere else. It runs no tests and applies no formatting — those belong to `/verify`.
- **No `CLAUDE.md`, no project memory under `.claude/memory/`, no hooks.** `/study` records; it does not act on the record. A finding that looks mechanically enforceable — "a hook could have caught this", "this rule misfired and should be amended" — is **flagged in the report for `/capture-learnings`** and left there. That command owns turning a learning into a rule fix, a gate, or a memory entry; see `.claude/commands/capture-learnings.md` for its order of preference. The division is deliberate: `/study` records what surprised the *agent* and only ever writes a record, where `/capture-learnings` starts from what the *invoker* corrected.
- **Nothing outward-facing.** No push, no PR, no merge.

---

## Final report

- **Evidence sources** — name each source from step 3 and its classification: which were `had`, which were `absent`, and which `errored`. Name every `errored` source with the command and the error text. This is what tells the invoker whether a thin record is thin-because-run-late or thin-because-nothing-happened, and it is stated here only — never in the file.
- **What was recorded** — each row's finding and level, or an explicit statement that nothing was recorded and why that is the normal outcome. On a re-run, say what was added and what was recognised as already present.
- **Anything flagged for `/capture-learnings`** — findings that look mechanically enforceable, which `/study` deliberately did not act on.
- **Any pre-existing working-tree changes** from the step-1 baseline to files other than `docs/study/<N>.md`, left untouched and named.
- Then exactly one of:
  - `STUDIED issue=<N> rows=<n>` — `rows=<n>` counts the rows this run added, so `rows=0` is the verdict for a clean run and for a re-run with nothing new. On `rows=0` nothing was written, so the tree is exactly as it was found — which is not the same as clean: if the step-1 baseline was non-empty, say so and name the paths, because `/verify` refuses to start on a dirty tree.
  - `FAILED reason=<short reason>` — a precondition or a step stopped the run. The repository is unchanged, unless the report explicitly names paths left modified. This never blocks anything else; it means only that no record was written.
