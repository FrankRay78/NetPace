# `/verify` reviews its own review fixes, in bounded rounds

**Intent:** A branch reported `VERIFIED` should contain no change that only the test suite has looked at. `/verify` applied its review findings and committed them with no reviewer in between, so the fix diff — often the riskiest code on the branch — reached the open PR unread, and the asynchronous `@claude` review was the first fresh look at it.

**Behaviour:**
- Given a branch whose first review finds something worth fixing, when `/verify` applies the fix, then the fix is put in front of a clean-context reviewer before the run ends, and the final report says how many rounds ran.
- Given a review fix that itself contains a confirmed problem, when the follow-up round reports it, then it is validated and resolved under the same severity policy as a first-round finding, and that resolution is reviewed in turn.
- Given a branch whose first review leads to no edits, when `/verify` finishes, then exactly one round ran and the run costs what it cost before this change.
- Given rounds that keep producing edits, when the bound is reached, then `/verify` reports `FAILED reason=review rounds did not converge`, names the findings still open and the commit no round read, and does not report the branch verified.

**Constraints:**
- `/verify` must still run to completion without prompting, and must terminate — it is stage 3 of `scripts/chain.sh`, which gates on its verdict line and kills it at a per-stage time limit.
- Step 0's clean-tree invariant ("after a review, anything in the tree is a review edit and nothing else") must survive the loop.
- `pr-review-toolkit:code-simplifier` edits files directly, so its edits are review edits under the same rule.
- The verdict vocabulary is fixed by `scripts/chain.sh`: a non-converged run has to be a `FAILED reason=` line, not a new third verdict.

**Decisions:**

*Observed failure (#239, PR #327).* `/verify`'s fix commit `6807df8` was 244 insertions across 12 files — the largest change on the branch, and the only one no reviewer read. Among them, `OoklaSpeedtest.ScreenServerAsync`'s blanket `catch (Exception)` was narrowed to `HttpRequestException or IOException or OperationCanceledException`. That let the `NotSupportedException` raised for a candidate URL that is not a web address escape and fail the whole server selection, where before such an entry was quietly ranked out. No test covered a malformed URL, so the suite stayed green and the branch was reported verified. The `Uri.TryCreate` guard and its regression test landed only after the PR review found it.

*Adopted — a bounded loop of rounds, each committed before the next.* Round one is the review `/verify` already ran; each later round reviews only what the previous round committed. Committing per round is what preserves the clean-tree invariant down the whole loop and what gives each round an exact diff rather than a reconstruction.

*Bound of three rounds, round one included.* A healthy run that needed a fix needs two rounds; a third absorbs one bad fix. Past that the fixes are not converging and the judgement belongs to a human, not to a fourth automated attempt. An unbounded loop was rejected outright: `/verify` runs unattended, so "repeat until clean" is a non-termination risk, not a quality setting. `VERIFY_LIMIT` in `scripts/chain.sh` is raised from 5400s to 16200s to match — the stage is now three passes long in the worst case, and leaving the limit at one pass would convert a converging run into a reported stall.

*Rejected — reverting a non-converged round's edits.* It would restore the "no unread change" property literally, but it discards real fixes and hands `/raise-pr` a branch in a worse state than `/verify` was given. Instead the edits stay committed, the tree stays clean, and the report names them as unreviewed with the verdict `FAILED`.

*Follow-up rounds use a narrower reviewer set over the fix diff only* — `code-reviewer` and `silent-failure-hunter` always, `pr-test-analyzer` when tests changed. Re-running all six reviewers over the whole branch each round would multiply the cost of every verify to re-read code a round has already cleared. `code-simplifier` is deliberately out: it is the one reviewer that edits files, so excluding it leaves nothing for the two-wave rule to sequence and a follow-up round is a single parallel wave.

*Rejected — exempting a trivial-looking fix round.* A round that only touched comments or docs arguably needs no follow-up, but an unattended run asked to judge "trivial" decides differently every time, and a comment that now misdescribes the code above it is precisely what a follow-up round catches. Every round with edits gets a follow-up round.

*Follow-up rounds are briefed with the diff and a question, not a conclusion.* A reviewer told "this fixes finding X" tends to confirm it. The round is given the diff range, the branch, and the state at the round's base, and asked what the changed code used to do — the question that would have exposed the narrowed `catch`.

*Rejected — adopting `pr-review-toolkit:review-pr`.* It orchestrates the same reviewers `/verify` already calls directly, so it would repeat round one rather than close this gap. Adding a review step to `/raise-pr` was rejected for the same reason plus a worse one: it is the irreversible stage, and the fix diff should be settled before anything is pushed.

**Date:** 2026-10-07
