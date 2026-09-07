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
   - Run `git rev-parse --abbrev-ref HEAD`. If it is `main`, STOP and report: "Run /study from a feature branch, not main." Write no file, create no commit, create no branch.

2. **Resolve the issue number.**
   - If the invoker gave one, normalise it (strip `#`, or take the trailing number of a URL) and use it.
   - Otherwise infer it from the branch name: `/build` creates `feature/<N>-<slug>`, so the leading number after the prefix is the issue.
   - If there is no argument and the branch name carries no issue number, STOP and report that the issue could not be inferred. Write nothing, commit nothing. This is the one case where the answer is genuinely unavailable, and guessing an issue number files the record against the wrong work.

3. **Resolve the issue title.** Run `gh issue view <N> --json title`. The title is needed for the H1 of a new file. If it cannot be fetched, STOP and report — an invented title mislabels the record permanently. (An existing file already carries its H1 and needs no re-fetch.)

4. **Gather the evidence, and note what you actually had.** Take whatever is available at the moment you run. Record which of these you had, because it is what tells a later reader whether a thin file is thin-because-run-late or thin-because-nothing-happened:
   - **The live conversation** — available only when `/study` runs in the same session as the work. It is the richest source and the only one that holds a mid-flight redesign that left no commit behind. It is also the one that evaporates: findings that exist only here are lost if the command is run later from a fresh session.
   - **The branch's commits** — `git log main..HEAD --oneline`. Revert pairs, a commit a later commit undoes, and a file rewritten repeatedly are all evidence of a surprise that cost rework.
   - **The diff** — `git diff main...HEAD --stat` for shape, and the diff itself where a specific finding needs grounding.
   - **PR review comments** — if the branch has a PR, `gh pr view --json comments,reviews`. This is where the asynchronous `@claude` review lands. Best-effort: no PR, or no review posted yet, is a normal no-op — never wait or poll for one.
   - **CI runs** — `gh run list --branch <branch>` and the logs of any failure. A check that failed on a clean runner after passing locally is the archetypal Environment row.

5. **Decide what, if anything, was genuinely surprising.** For each candidate, apply both tests before it earns a row:
   - **Was it a surprise?** Something that was not anticipated by the issue as written and by a competent reading of the code. Work proceeding as expected is not a surprise, however much of it there was. A reviewer finding that had to be actioned *is* a surprise — it is exactly the kind of routine cost the accumulated tally exists to measure, and excluding it would leave the Execution count meaningless.
   - **Can you point at the evidence?** Name the commit, the review comment, the failing test, the CI run. The Finding text carries its source inline where it has one — e.g. `Reviewer (silent-failure-hunter): …`, `CI (ubuntu-latest): …`. There is no separate evidence column; the row grounds itself in its own prose.

   If nothing passes both tests, go to step 8 with zero rows. That is the expected outcome for a clean run.

   There is **no cap** on rows. Record every finding that passes both tests.

6. **Write the record.** `docs/study/<N>.md`, one file per issue.
   - **New file** — H1 `# <N> — <issue title>`, then a single table:

     ```markdown
     # 260 — Add /study — record what surprised a build, so the harness can be improved from evidence

     | Finding | Level | Fix applied |
     | --- | --- | --- |
     | … | … | … |
     ```

   - **Existing file** — append new rows to the same flat table. Do **not** add a per-pass section, a date, or a pass identifier: a reader tallying levels never needs to know which pass wrote a row, and per-pass headings would fragment the table they are tallying.
   - **Dedup is a judgement call against the existing row text.** Read the rows already there; a finding already recorded is not recorded again, even if this run reached it by a different route. Leave every existing row exactly as it is — `/study` appends, it never edits or removes.
   - Write markdown one line per row and paragraph; no hard column wrapping.
   - If `docs/study/README.md` does not exist, create it — a reader cannot interpret the levels without it.

7. **Commit what you wrote, to the current feature branch.** The commit lands on the branch you are already on — never a new branch, never `main` — so the record travels with the work's pull request rather than arriving separately.
   - Stage the study record by **explicit path** — `git add docs/study/<N>.md` (plus the README if you created it) — never `git add -A`, which would sweep unrelated working-tree changes into a documentation commit.
   - Commit in imperative mood referencing the issue: `Refs #<N>: record study findings`. Use `Refs #<N>`, never a closing keyword — a closing keyword would close the issue the moment the commit reached `main`.
   - Leave the tree clean of anything `/study` wrote, so `/verify` can run immediately afterwards without complaint.
   - If the working tree carried changes before `/study` ran, leave them exactly as they were and name them in the report. They are not `/study`'s to commit.

8. **Nothing to record.** If step 5 found nothing, or step 6 found every candidate already present in the file: write no file, make no edit, create no commit. Leave the working tree exactly as you found it and say so plainly in the report. Do not create an empty file, and do not add a "nothing notable" row.

9. **Stop here.** Do not push, do not open or modify a pull request, do not merge, do not run any other command.

---

## What `/study` must never touch

- **No source, test, or configuration file.** `/study` writes under `docs/study/` and nowhere else. It runs no tests and applies no formatting — those belong to `/verify`.
- **No `CLAUDE.md`, no project memory under `.claude/memory/`, no hooks.** `/study` records; it does not act on the record. A finding that looks mechanically enforceable — "a hook could have caught this", "this rule misfired and should be amended" — is **flagged in the report for `/capture-learnings`**, which is the command that owns turning a learning into a rule or a gate. That division is deliberate: `/study` captures what surprised the *agent* and only ever writes a record, where `/capture-learnings` captures corrections the *invoker* gave and prefers converting them into hooks or tests.
- **Nothing outward-facing.** No push, no PR, no merge.

---

## Final report

- **Evidence sources available to this run** — name which of the five in step 4 you actually had, and which you did not. A run from a fresh session says so; a run before any PR exists says so.
- **What was recorded** — each row's finding and level, or an explicit statement that nothing was recorded and why that is the normal outcome. On a re-run, say what was added and what was recognised as already present.
- **Anything flagged for `/capture-learnings`** — findings that look mechanically enforceable, which `/study` deliberately did not act on.
- **Any pre-existing working-tree changes** left untouched.
- Then exactly one of:
  - `STUDIED issue=<N> rows=<n>` — `rows=<n>` counts the rows this run added, so `rows=0` is the verdict for a clean run and for a re-run with nothing new. On `rows=0` the working tree is untouched; otherwise the record is committed and the tree is clean.
  - `FAILED reason=<short reason>` — a precondition or a step stopped the run. The repository is unchanged. This never blocks anything else; it means only that no record was written.
