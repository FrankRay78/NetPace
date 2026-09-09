# Study records

Each file in this folder records what *surprised* one piece of work: a mid-flight redesign, a reviewer finding that had to be actioned, an acceptance criterion that turned out ambiguous. One file per issue, named `<issue number>.md`, written by the `/study` command.

Individually a study record is a footnote. Collectively they answer a question no single pull request can: **which part of the harness keeps costing us, and therefore what to fix next.**

## What a file looks like

An H1 naming the issue, then one flat table:

```markdown
# <N> — <issue title>

| Finding | Level | Fix applied |
| --- | --- | --- |
| Reviewer (silent-failure-hunter): the retry path swallowed a cancellation | Execution | Rethrew `OperationCanceledException` before the catch-all |
```

The table is flat and append-only. There are no per-pass sections, no dates and no identifiers, because a reader tallying levels never needs to know which pass wrote a row — and per-pass headings would fragment the very table they are tallying. `/study` can be run repeatedly against the same issue; a later run appends only what is new and leaves existing rows untouched.

There is no separate evidence column. The Finding text names its own source inline where it has one — `Reviewer (…):`, `CI (…):`, a commit — which keeps a row auditable without a column that would be empty half the time.

## The four levels

Every row is classified to exactly one level. The levels are not severities. They say **where the fix belongs**, which is what makes a recurring level actionable rather than merely interesting.

| Level | What it means | What a recurring one implies |
| --- | --- | --- |
| **Execution** | The goal and the written criteria were right; the doing went wrong. | Improve the command prompts, the gates, or the tooling. |
| **Plan-spec** | The criteria were wrong, ambiguous, or incomplete. | Improve how issues are drafted upstream. |
| **Codebase** | The doing and the criteria were both sound; the existing code was not what anyone assumed — a hidden coupling, a landmine, debt that made the work harder than the criteria implied. | A refactor, or issues raised against the code itself. |
| **Environment** | The surprise came from outside the repository: a check that failed on a clean runner after passing locally, a tool or service that was unavailable, a dependency that moved. | Harden the environment, or the way the harness depends on it. |

**Codebase vs Execution is the judgement call**, and it will sometimes be got wrong. The test is where the *fix* belongs: a prompt or gate change is Execution; a change to code that was already there is Codebase.

**There is no *Goal* level** — no row for "this work should not have been done at all". Whether an issue should have existed is settled when the issue is drafted, not downstream while building it, so that judgement is not available to the thing doing the work: the level would sit empty and dilute every tally it appeared in. A level nobody can populate is worse than no level. (Issue #260 records the evidence this was decided on.)

## How to read the files back

Reading is done offline, by a human. `/study` writes; it never tallies, and it never draws a conclusion from what it wrote.

1. **Tally the levels across every file.** The shape of the distribution is the finding — a large Execution count against a small Plan-spec count says the doing is lossy while the specs are mostly sound, and points the next improvement at the command prompts rather than at how issues are drafted.
2. **Read the Finding text within the dominant level, looking for a repeated shape.** A level that is large because of twenty unrelated one-offs means something different from one that is large because the same class of mistake recurred; only the second names a specific fix.
3. **Recover the denominator from merged-PR history.** A clean run writes nothing, so the absence of a file is not evidence of anything by itself — it means either that the work held no surprises or that `/study` was never run on it. The count of merged PRs is what turns a raw tally into a rate.
4. **Check for a level that never populates.** An empty level dilutes every tally it appears in, and is a candidate for removal on the same grounds that removed *Goal*.

## Why the folder is all-signal

Two guards keep it that way, and both matter more than completeness:

- **A clean run writes nothing.** There is no "nothing notable" record, so the presence of a file means the work taught us something.
- **Every row is grounded in evidence** — a commit, a review comment, a failing test, a CI run. Asked what surprised it, a model will readily invent plausible surprises; requiring evidence is what stops the folder filling with confabulation and becoming worse than no record at all.

## Related

- [`.claude/commands/study.md`](../../.claude/commands/study.md) — the command that writes these files.
- [`.claude/commands/capture-learnings.md`](../../.claude/commands/capture-learnings.md) — the adjacent, deliberately different reflection step. `/study` records what surprised the *agent* and only ever writes a record; `/capture-learnings` starts from what the *invoker* corrected, and its order of preference is to fix the rule that misfired, then to enforce it deterministically, and only then to write a memory entry. A study finding that looks mechanically enforceable is flagged for `/capture-learnings` rather than acted on here.
- [`docs/agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md) — the surrounding workflow these records reflect on.
