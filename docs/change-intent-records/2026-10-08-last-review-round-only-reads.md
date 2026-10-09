# `/verify`'s last review round only reads, and its findings decide

**Supersedes:** [2026-10-07-verify-reviews-its-own-fixes.md](2026-10-07-verify-reviews-its-own-fixes.md) — "Bound of three, round one included" and its sizing of `VERIFY_LIMIT`. The bound is four, the last round may not fix, and the limit is measured rather than multiplied.

**Intent:** A `/verify` that reaches its round bound should fail only over something a reviewer actually found. Under the old bound the third round's fix was committed with no round left to read it, so the run had to fail whatever that fix was — on #328's own branch, over one deleted clause, after two settling rounds.

**Behaviour:** `.claude/commands/verify.md` steps 2–3. Rounds one to three may fix; a fourth round reviews the third fix and makes no edit. A material finding there is `FAILED reason=last review round found a material problem`; otherwise the branch is verified with that round's minor findings listed as not fixed.

**Constraints:** `/verify` runs unattended as stage 3 of `scripts/chain.sh`, so it must terminate and stay within the verdict vocabulary the chain reads. (Since #345 the chain reads that verdict off the report's last non-blank line rather than scanning the prose for it; the vocabulary is unchanged, the mechanism is not.)

**Decisions:**

- *Last round reads, rather than raising the bound again.* Raising it moves the same problem one round out; a read-only last round removes it, and costs one extra review instead of one extra review-plus-fix-plus-suite-run.
- *Materiality is the reviewer's rating, not the orchestrator's.* Any reviewer's confirmed Blocker/Important in the branch's own code, or caused by it, fails the run. The orchestrator may reject a finding as false; it may not re-rate a real one. Otherwise the thing deciding the verdict is the thing that wants the run to finish.
- *Rejected: comparing each round's fix size with the last.* A five-line fix followed by an eight-line one says nothing about whether the branch is sound, least of all when the lines are comments.
- *The `(round <n>, unreviewed)` commit marker and the `review rounds did not converge` verdict are gone.* Both described a state the loop can no longer reach. Reviewer-error and time-limit stops can still leave an unread commit and keep their reporting.
- *`VERIFY_LIMIT` 16200s → 7200s, sized from a measurement.* #328's chain ran three full rounds in ~48 minutes; four rounds, the last without a fix, land near an hour. The limit is the stage's only stall detector, so headroom comes from a measured run rather than a per-round multiplier — and a hung `/verify` is now reported after 2h instead of 4h30m.
- *Formatting also runs inside each round that applies edits, using CI's bare `dotnet format ./src/NetPace.sln`.* A single up-front pass never sees a review fix, and the narrower `style`/`whitespace` pair is not the check CI enforces — together, a branch could be verified here and fail its own pull request's format check. Formatting before the round's suite re-run keeps the gate on exactly the committed bytes.

**Date:** 2026-10-08
