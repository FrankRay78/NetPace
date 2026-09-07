# Renaming the retired `ship` orchestrator to `/verify`, and cutting its pull-request step

**Intent:** Make the SDLC commands a chain of independently invocable stages — `/build` → `/verify` → `/raise-pr` — by cutting the pull-request step off the single orchestrator that previously did both jobs, and renaming what remains for what it actually guarantees. The retired name was `ship`.

**Behaviour:**
- Given a feature branch with commits over `main` and a clean tree, when `/verify` runs to completion, then the suite has run green, every confirmed blocker is resolved, warranted fixes are committed, any finding deferred as out-of-scope is named in the report, the tree is clean, and no branch has been pushed and no PR opened.
- Given `/verify` has just reported success, when `/raise-pr` is invoked separately, then it pushes and opens the PR with no further preparation of the branch.
- Given `main` is checked out, or the branch has no commits over `main`, or the tree is dirty, when `/verify` is invoked, then it stops before running the suite or spawning reviewers and reports which precondition failed.

**Constraints:**
- Raising the PR is the only irreversible, outward-facing act in the chain. Everything before it is safe to re-run; pushing a branch and opening a PR is not. That asymmetry is the whole reason the cut lands here and nowhere else.
- `/verify`'s exit state — green, reviewed, fully committed, clean tree — was already exactly `/raise-pr`'s entry condition, so the two compose by hand with nothing in between.
- Three rationale passages in the `ship` command justified themselves by the fact that the orchestrator *called* `/raise-pr`. A verbatim rename would have left all three wrong, so each was re-argued rather than swapped. The precondition guard lost its "don't let `/raise-pr`'s late branch check be the first line of defence" argument and now stands on cost alone. Review B moved out of the gate entirely, since the PR stage requests it. And the no-docs-only-skip argument **inverted**: it used to lean on the `gh pr create` hook re-running the suite later in the same command, and that hook now sits in a stage that may never run — so the verify-time run became the load-bearing one rather than the redundant one. An inverted argument is the case a verbatim rename silently gets wrong, which is why they were re-argued individually.
- The commit-the-fixes step (step 3) keeps its "because `/raise-pr` pushes *commits*" argument, and is now more load-bearing, not less: the gap between the review fix and the push is open-ended, so an uncommitted edit has an unbounded window in which to be lost.
- After the split, `/verify`'s closing report is the **only** route by which a deferred out-of-scope finding can reach a PR body, because `/raise-pr` no longer runs in the same session. The report requirement is therefore a real mechanism, not a courtesy.

**Decisions:**

*Rejected — keeping the retired name.* After the cut the command formats, tests, reviews and commits; it no longer ships anything, so `ship` over-promises exactly the step that was removed. `verify` describes what it actually guarantees.

*Rejected — naming it `/test`.* Someone typing `/test` expects `dotnet test`. This command reformats the tree and lets reviewer subagents edit code, which is a much larger contract than the name would advertise.

*Rejected — auto-invoking `/raise-pr` from `/verify` behind a flag.* It would have preserved the one-invocation convenience, and it is the option a future maintainer is most likely to reach for. It loses because the point of the split is that the outward-facing step is *always* deliberate; a flag reintroduces a path where a PR is opened as a side effect of a verification run — and a default-off flag set once in a loop config is indistinguishable, at the call site, from the unsplit command.

*Rejected — splitting `/verify` further into `/format` and `/review` stages.* The format → suite → review ordering is a hard structural gate: review cannot begin against unverified or red code because it literally runs after the exit code. Separating those stages would demote that structure to a convention a human has to remember in the right order.

*Rejected — a hook enforcing the new chain ordering.* The suite's exit code remains the gate. A hook that watches for the agent *claiming* to have verified is the "gates attach to actions, not prose" anti-pattern the workflow guide warns against.

*Adopted — renaming the vocabulary, not just the command.* The retired stage vocabulary — the gate, and the formatting cadence, both of which were named after the old command — is renamed to *verify* throughout, including in the otherwise project-agnostic [`../agentic-workflow.md`](../agentic-workflow.md). One vocabulary, matching the one command that exists, so a reader meeting the term anywhere in the docs is pointed at something invocable. The records in this folder, three `.claude/memory/` entries and three hook comments were updated the same way. Where a sentence narrates a dated event rather than a live mechanism — the two that cite issue #122 — it keeps the retired name in a parenthetical, so the claim stays checkable against the issue it names.

*Explicitly not renamed — `ship` as an ordinary English verb.* It is used throughout the repo for releasing software to users ("they ship to NuGet consumers", "must not ship to end users", "ship unverified") and is unrelated to the stage name. A blanket find-and-replace over the word corrupts these; the rename targeted the stage name only.

**Date:** 2026-09-07
