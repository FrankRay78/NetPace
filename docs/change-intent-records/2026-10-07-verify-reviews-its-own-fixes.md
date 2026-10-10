# `/verify` reviews its own review fixes, in bounded rounds

**Supersedes:** [2026-09-25-verify-reviewer-waves.md](2026-09-25-verify-reviewer-waves.md) — "Step 3 is untouched". Step 3 now commits per round and decides whether another round runs; the two-wave rule governs round one only.

**Superseded by:** [2026-10-10-verify-reviews-once.md](2026-10-10-verify-reviews-once.md) — the review loop this record introduced. `/verify` now reviews once, and the pull-request review reads its fix. Read this record as history. (The [2026-10-08](2026-10-08-last-review-round-only-reads.md) record had earlier replaced its bound and `VERIFY_LIMIT` sizing.)

**Intent:** A branch reported `VERIFIED` should contain no change that only the test suite has looked at. On #239 / PR #327, `/verify`'s fix commit narrowed a blanket `catch` in `OoklaSpeedtest.ScreenServerAsync`, letting a non-HTTP server URL fail the whole selection. No reviewer read it and no test covered it, so the branch was reported verified.

**Behaviour:** The mechanics live in `.claude/commands/verify.md` steps 2–3. In short: each round's edits are committed and the next round reviews exactly that commit; the loop ends when a round changes nothing, or `FAILED reason=review rounds did not converge` after three rounds.

**Constraints:** `/verify` runs unattended as stage 3 of `scripts/chain.sh`, so it must terminate, and its verdict must stay within the vocabulary `chain.sh` reads. (Since #345 that verdict is read off the report's last non-blank line rather than scanned for in the prose.)

**Decisions:**

- *Commit then review, not review then commit.* #328's first criterion literally asked for the latter. Reviewing the uncommitted tree loses attribution of edits to rounds and reopens the tree after the last suite run. The issue's Confirmed decisions reworded the criterion to "no `VERIFIED` branch contains a change no reviewer read"; that reworded criterion is the one delivered.
- *Bound of three, round one included; no unbounded loop.* An unattended "repeat until clean" is a non-termination risk. `VERIFY_LIMIT` rises from 5400s to 16200s to match; the cost is that a genuinely hung `/verify` now takes 4h30m to be reported, since the timeout is the only stall detector.
- *Non-converged edits stay committed, marked `unreviewed`, not reverted.* Reverting discards real fixes and hands `/raise-pr` a worse branch.
- *No exemption for a trivial-looking fix round.* An unattended run judges "trivial" inconsistently, and a comment that misdescribes the code above it is exactly what a follow-up round catches.
- *Follow-up rounds use a narrower reviewer set over the fix diff, briefed without the conclusion.* `/review-slop` is excluded on cost alone, so it never sees a fix diff — an accepted gap.
- *Rejected: `pr-review-toolkit:review-pr`* (repeats round one) *and a review step in `/raise-pr`* (the irreversible stage; the fix diff should be settled before anything is pushed).

**Date:** 2026-10-07
