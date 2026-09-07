# Renaming the retired `ship` orchestrator to `/verify`, and cutting its pull-request step

**Intent:** Make the SDLC commands a chain of independently invocable stages — `/build` → `/verify` → `/raise-pr` — by cutting the pull-request step off the single orchestrator that previously did both jobs, and renaming what remains for what it actually guarantees. The retired name was `ship`; it is named here only as the subject of the rename, and is no longer invocable.

**Behaviour:**
- Given a feature branch with commits over `main` and a clean tree, when `/verify` runs to completion, then the suite has run green, review findings are resolved and committed, the tree is clean, and no branch has been pushed and no PR opened.
- Given `/verify` has just reported success, when `/raise-pr` is invoked separately, then it pushes and opens the PR with no further preparation of the branch.
- Given `main` is checked out, or the branch has no commits over `main`, or the tree is dirty, when `/verify` is invoked, then it stops before running the suite or spawning reviewers and reports which precondition failed.

**Constraints:**
- Raising the PR is the only irreversible, outward-facing act in the chain. Everything before it is safe to re-run; pushing a branch and opening a PR is not. That asymmetry is the whole reason the cut lands here and nowhere else.
- `/verify`'s exit state — green, reviewed, fully committed, clean tree — was already exactly `/raise-pr`'s entry condition, so the two compose by hand with nothing in between. The split needed no adapter step.
- Three rationale passages in the old command justified themselves by the fact that the orchestrator *called* `/raise-pr`. A verbatim rename would have left all three wrong, so each was re-argued rather than swapped:
  - The step-0 precondition guard was justified by "`/raise-pr`'s own late branch check must not be the first line of defence". That no longer applies; the guard now stands on cost alone — three cheap `git` calls versus minutes of format, suite and review.
  - Step 1b's refusal to skip the suite on docs-only branches leaned on the `gh pr create` `PreToolUse` hook re-running it "at step 4 anyway". That hook now fires inside a separately invoked `/raise-pr` which may not follow for a long while, or at all — so the argument **inverts**: the step-1b run becomes the chain's only unconditional whole-suite gate, which strengthens the no-skip conclusion rather than weakening it.
  - Review B is now requested by `/raise-pr` when a human runs it, placing it outside the verify gate entirely.
- Step 3's "commit the edits, because `/raise-pr` pushes *commits*" survives and is now more load-bearing, not less: the gap between the review fix and the push is open-ended, so an uncommitted edit has an unbounded window in which to be lost.
- After the split, `/verify`'s closing report is the **only** route by which a deferred out-of-scope finding can reach a PR body, because `/raise-pr` no longer runs in the same session. The report requirement is therefore a real mechanism, not a courtesy.

**Decisions:**

*Rejected — keeping the retired name.* After the cut the command formats, tests, reviews and commits; it no longer ships anything, so `ship` over-promises exactly the step that was removed. `verify` describes what it actually guarantees.

*Rejected — naming it `/test`.* Someone typing `/test` expects `dotnet test`. This command reformats the tree and lets reviewer subagents edit code, which is a much larger contract than the name would advertise.

*Rejected — auto-invoking `/raise-pr` from `/verify` behind a flag.* It would have preserved the one-invocation convenience, and it is the option a future maintainer is most likely to reach for. It loses because the point of the split is that the outward-facing step is *always* deliberate; a flag reintroduces a path where a PR is opened as a side effect of a verification run, which is the failure the split exists to prevent.

*Rejected — splitting `/verify` further into `/format` and `/review` stages.* The format → suite → review ordering is a hard structural gate: review cannot begin against unverified or red code because it literally runs after the exit code. Separating those stages would demote that structure to a convention a human has to remember in the right order.

*Rejected — a hook enforcing the new chain ordering.* The suite's exit code remains the gate. A hook that watches for the agent *claiming* to have verified is the "gates attach to actions, not prose" anti-pattern the workflow guide warns against.

*Adopted — renaming the vocabulary, not just the command.* The retired stage vocabulary — the gate, and the formatting cadence, both of which were named after the old command — is renamed to *verify* throughout, including in the otherwise project-agnostic [`../agentic-workflow.md`](../agentic-workflow.md). One vocabulary, matching the one command that exists, so a reader meeting the term anywhere in the docs is pointed at something invocable. The historical records in this folder were updated the same way: each surviving reference now names the stage that owns the behaviour it describes — `/verify` for the review and test-green gate, `/raise-pr` for the push.

*Explicitly not renamed — `ship` as an ordinary English verb.* It is used throughout the repo for releasing software to users ("they ship to NuGet consumers", "must not ship to end users", "ship unverified") and is unrelated to the stage name. A blanket find-and-replace over the word corrupts these; the rename targeted the stage name only.

**Date:** 2026-09-07
