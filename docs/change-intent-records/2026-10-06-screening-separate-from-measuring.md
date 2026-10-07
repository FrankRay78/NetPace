# Screening a server is a different job from measuring it

**Intent:** Stop server selection rejecting servers that are plainly reachable, and make "fastest server by latency" actually compare the candidates it discovered — while keeping a hard ceiling on how long choosing a server can take.

**Behaviour:** Given several servers are discovered and at least one is reachable, when a run selects a server, then every discovered server is screened, the fastest that answered is chosen, its latency is measured with the full probe, and the measured figure is reported. Given no discovered server answers, when a run selects a server, then it reports `No servers available` with its exit code unchanged. Given `servers -l`, then each server shows the screening latency auto-selection would rank it on, or `-` if it could not be reached.

**Constraints:** The ceiling must stay — before budgets existed, a server answering in 20–60 s per request could stall a run for minutes. The user-visible "no servers" message and exit code must not change. `--server` must keep bypassing selection.

**Decisions:**

- **Screening reinterprets `GetFastestServerByLatencyAsync` rather than adding an interface method.** A per-server screening-result method was considered and rejected: it is public API surface and a MINOR bump for a capability the existing method's name already describes. The console calls `GetServerLatencyAsync` on the winner for the figure it reports, so the two jobs stay distinct without the interface growing.
- **At the ceiling, outstanding candidates are abandoned, not awaited.** Awaiting the slowest in-flight request would make the ceiling advisory, and under a handler that ignores cancellation it would not bound anything at all. The ceiling is the one guarantee this change must not weaken, so it is enforced as wall-clock.
- **The screening figure is the fastest completed request, not the average.** Screening has no warm-up, so the first request carries connection setup that the link is not responsible for; averaging it in would penalise every server equally but noisily, for a figure whose only job is to rank.
- **A winner that fails its full measurement fails the run.** Falling back to the next-ranked candidate was considered and rejected: a run must not swap its server out by going back to a previous step, or a reported result describes two servers rather than one.
- **`servers -l` keeps its discovered row order.** Sorting the table fastest-first with unreachable servers last was considered and rejected as a change to existing behaviour beyond this issue's purpose; "ranked" means the figures are visible and comparable.
- **The "nothing answered" case keeps throwing a bare `Exception`.** A specific type would better satisfy Constitution Principle V, but the message is load-bearing — the console prints it verbatim — and changing the type here buys nothing the message does not already carry.

See [docs/architecture/server-screening-and-measurement.md](../architecture/server-screening-and-measurement.md) for the resulting design.

**Date:** 2026-10-06
