# The Byte Cap Race Is Benign By Construction

**Intent:** Keep the unsynchronised read of `wasCancelledLocally` in `SpeedTestProviderBase.GenericTestSpeedAsync`, and record the argument that makes it safe, so a later edit cannot quietly invalidate it.

**Behaviour:** A request cancelled because the byte budget was reached is excluded rather than counted as failed. See issue #588 for why the argument moved out of the code comment.

**Constraints:** The read is safe only while two things hold. `cts.Cancel()` stays inside the same `lockObject` the finally block's `if (!requestExcluded)` gate acquires, so no thread can pass the gate between the flag being set and cancellation. And every counter update — `completedCount`, `failedCount`, `succeededCount`, `budgetBytesReturned` — stays inside that gate. Any request whose counts are applied therefore read the flag before cancellation, and cancellation is monotonic, so a stale `false` can only set `requestFailed` on a request whose counts are discarded anyway.

**Decisions:**

- Read the flag without the lock. Rejected: taking `lockObject` on the exception path. The gate, not the flag, is what authoritatively excludes a request, so the lock would buy nothing and would hold the lock across a caller-supplied clock read.
- Bool loads are atomic in .NET, so staleness is the only failure mode to account for; tearing is not.

**Date:** 2026-11-24
