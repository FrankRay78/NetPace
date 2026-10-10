# `/verify` reviews once, and the pull-request review reads the fix

**Supersedes:** [2026-10-07-verify-reviews-its-own-fixes.md](2026-10-07-verify-reviews-its-own-fixes.md) and [2026-10-08-last-review-round-only-reads.md](2026-10-08-last-review-round-only-reads.md) — the review loop itself, its bound, and the read-only last round. Also the round-four column of [2026-10-09-a-rating-is-raised-never-lowered.md](2026-10-09-a-rating-is-raised-never-lowered.md); its rule that a rating is never lowered stands.

**Intent:** A ready issue should reach a pull request with a review on it, at a cost that lets several through one usage window. The loop that re-reviewed `/verify`'s own fixes was stopping that: it mostly found defects in text it had just written, and runs ended at the bound or out of allowance.

**Behaviour:** `.claude/commands/verify.md` steps 2–3. Issue #362 carries the measurements.

**Constraints:** `scripts/chain.sh` is unchanged apart from comments, so the verdict vocabulary and stage order stay as they were.

**Decisions:**

- *One round, rather than a loop with a better stopping rule.* Narrower follow-up rounds and a lower severity for prose findings were considered. Both keep the round bookkeeping and still need a round that finds nothing; one round needs neither.
- *The pull-request review is the reader for the fix commit.* This reverses the 2026-10-07 record, which was written because a narrowed `catch` clause reached a PR unread by anything but the suite. That risk is accepted again. The pull-request review reads the whole diff, but it is lighter than a `/verify` round and runs nothing.
- *Smallest-change fixes are the remaining guard.* The fix commits on #268 added 738 lines to a 2,358-line branch. A fix held to the lines that are wrong leaves less for anyone to miss.
- *Unfixed findings end with `/verify`'s report.* The maintainer's call: carrying them to the pull request would tell its reviewer what to find. `/verify` no longer raises follow-up issues for them either.
- *Suggestions stay discretionary.* Round one was not what failed, so its rules are unchanged apart from the smallest-change rule.
- *A reviewer's own commit no longer stops the run.* It is formatted, tested and left on the branch with the rest, since the pull-request review reads it like any other commit.
- *`VERIFY_LIMIT` stays at 2h.* #268's round took about 40 minutes; the limit is not tightened on one measurement.

**Date:** 2026-10-10
