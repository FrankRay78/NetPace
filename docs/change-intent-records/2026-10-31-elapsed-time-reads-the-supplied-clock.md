# Elapsed Time Reads The Supplied Clock

**Intent:** Make the time provider a caller passes to `OoklaSpeedtest` govern every elapsed-time measurement it takes, not just the timestamp on a diagnostic record, so a consumer or a test that controls the clock gets exact figures.

**Behaviour:** See [#456](https://github.com/FrankRay78/NetPace/issues/456).

**Constraints:**

- Measurement follows the supplied clock; waiting does not. The screening ceiling, the per-request HTTP timeouts and the pause between latency probes stay on the system clock, because a controlled clock that stood still would suspend them (#456, confirmed decisions).
- No public member was added. The constructor parameter already existed; only what it governs widened.

**Decisions:**

- `TimeProvider.GetTimestamp`/`GetElapsedTime` as the seam. Rejected: a NetPace-owned stopwatch abstraction — `TimeProvider` already carries a monotonic timestamp any consumer can substitute, and a second seam would need a second constructor parameter to inject it through.
- A hand-rolled controllable clock in the test project. Rejected: `Microsoft.Extensions.TimeProvider.Testing` — a package dependency for a twenty-line double whose only requirement is to move when a test tells it to.
- Tests order concurrent screening responses behind a gate. Rejected: letting candidates race on one controlled clock — an advance made for one candidate lands inside another's measurement and neither figure is predictable.

**Date:** 2026-10-31
