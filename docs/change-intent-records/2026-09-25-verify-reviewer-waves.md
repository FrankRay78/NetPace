# Sequencing `/verify`'s reviewers: five reporters in parallel, then `code-simplifier`

**Intent:** Every `file:line` in a `/verify` review report should point at the code the finding is about. With all six `pr-review-toolkit` reviewers launched together over one working tree, the one that edits files (`code-simplifier`) rewrote files the other five were still reading, so their anchors drifted between the edited file and HEAD.

**Behaviour:**
- Given a feature branch and a clean working tree, when `/verify`'s review step runs, then no file under review changes while any reporting reviewer (or `/review-slop`) is still reading it.
- Given `code-simplifier` has made edits in the review step, when step 3 runs, then those edits are in the working tree and are committed alongside the loop's own fixes, exactly as before.

**Constraints:**
- Step 3's invariant: because step 0 required a clean tree, `git status --porcelain` shows exactly what step 2 changed from *any* source, including files `code-simplifier` edited directly. Whatever the fix, simplifier edits must land in the same working tree.
- Which reviewers run is settled (#265); the six stay, and the severity policy and `/review-slop` are unchanged.
- The cost is one extra serial step. #292 estimates, from the #265 run 2 measurement, that the five reporters take ~260 s wall-clock in parallel and the simplifier ~156 s.

**Decisions:**

*Observed failure (#265, run 2, `feature/243-maintenance-backlog`).* `code-simplifier` edited `CSVConsoleWriter.cs` while the others were mid-review. Two reviewers noticed the foreign edit and reviewed HEAD instead; the other three said nothing, and their line references split — `:27-28` (the edited file) against `:30-31` (HEAD). No finding was wrong in substance, but nothing failed and none of the misled reviewers could tell.

*Rejected — giving `code-simplifier` its own worktree.* It is the obvious fix and it breaks step 3: edits made in another worktree never appear in this tree's `git status --porcelain`, so `git add -A` never stages them and they never reach the PR.

*Fallback, not adopted — making `code-simplifier` report-only.* `/verify`'s main loop would apply its suggestions the way it applies every other finding, keeping all six concurrent and step 3 intact. It works against the agent's defined behaviour of editing directly, so it is the less robust option; reach for it only if sequencing proves awkward.

*Adopted — two waves.* The five reporters run in parallel (with `/review-slop` alongside), and `code-simplifier` runs alone once all of them have returned. The main loop applies its own fixes only after the simplifier returns, relocating each HEAD-anchored finding in the edited tree first, so no writer overlaps a reader or another writer. Step 3 is untouched.

**Date:** 2026-09-25
