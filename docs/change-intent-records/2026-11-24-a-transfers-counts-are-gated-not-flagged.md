# A Transfer's Counts Are Gated, Not Flagged

**Intent:** Record why `GenericTestSpeedAsync` reads `wasCancelledLocally` without holding `lockObject`, and what that argument rests on, so a later edit cannot quietly invalidate it.

**Behaviour:** See CIR [a-figure-counts-what-the-link-delivered](2026-11-15-a-figure-counts-what-the-link-delivered.md) and issue [#534](https://github.com/FrankRay78/NetPace/issues/534) for what the two totals report.

**Constraints:**

- The authoritative exclusion is the `if (!cts.IsCancellationRequested)` gate inside `lock (lockObject)`, which guards `completedCount`, `failedCount`, `succeededCount` and `budgetBytesReturned` together. `wasCancelledLocally` only classifies a request whose counts that gate may then discard.
- The flag is set and `cts.Cancel()` called on the next line inside the same lock the gate takes, so no thread passes the gate between the two. A request whose counts are applied therefore ran its gate before cancellation, so its unlocked read was not stale; cancellation is monotonic, so there is no path back to `false`.
- **The argument breaks if `cts.Cancel()` moves out of that lock, or if any counter update moves outside the gate.**

**Decisions:**

- An unlocked `bool` read. Rejected: taking `lockObject` on the failure path — it would serialise every failing request behind the finishing ones for a read that cannot tear and whose staleness only mislabels a request already discarded.
- Classifying a request before reading the clock. Rejected: reading the elapsed figure first — `ElapsedMillisecondsSince` calls a caller-supplied `TimeProvider`, so it can throw, and an unclassified request would then be counted as a success by the `finally` block.

**Date:** 2026-11-24
