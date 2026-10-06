# Server Screening and Measurement

> How NetPace chooses a speed test server, why choosing and measuring are two different jobs, and why the ceiling on choosing must stay.

**Scope**: `OoklaSpeedtest.GetFastestServerByLatencyAsync`, `ServerDiscoverySettings`, `ServerSelector`, `ListServersCommand`
**Audience**: anyone changing server selection, the latency probe, or the settings that bound either
**Issue**: [#239](https://github.com/FrankRay78/NetPace/issues/239)

---

## The split

Choosing a server and measuring a server are two different jobs with two different accuracy requirements, and NetPace treats them that way.

**Screening** answers "is this server reachable, and roughly how fast?" for every discovered candidate. It is a ranking pass: a few requests per server with no deliberate waiting between them, run concurrently across all candidates, under one overall ceiling. It is cheap on purpose, because it runs against every server on the list.

**Measuring** answers "what is the latency of this server?" to the accuracy NetPace reports. It is the probe NetPace has always had — a fixed number of requests spaced by a fixed interval, averaged — and it runs exactly once, on the winner.

`GetFastestServerByLatencyAsync` is the screening pass and returns the winner carrying its *screening* latency. The console then measures that server with `GetServerLatencyAsync`, and the measured figure is the one reported. The interface did not change: screening is a reinterpretation of what "find the fastest server" already meant, not a new method.

## Screening ranks, it does not reject

A server is **reachable** when at least one of its screening requests completes inside the ceiling. A reachable but slow server is a valid candidate that simply loses. Only a server that produced no completed request at all drops out.

That distinction is the whole bug this design replaced. Selection used to apply a per-candidate budget — the discovery timeout for the first candidate, then a fraction of the best latency seen so far, with a floor well below the probe's own minimum runtime — to a *full latency measurement*. Three things followed:

- A server the `servers -l` table showed as reachable at 66 ms or 106 ms could not qualify.
- Every candidate after the first was judged against a budget shorter than the probe could possibly complete in, so it always failed. "Fastest server by latency" compared exactly one server — whichever answered first — and the ranking was an illusion.
- When no candidate cleared the first budget, the whole run ended with "No servers available". A modestly slow link was enough to trigger it, so it was a routine outcome rather than an edge case.

The fix is not a bigger budget. It is applying the ceiling to a unit of work cheap enough to fit under it.

## The ceiling exists, and must stay

`ServerDiscoverySettings.ServerTimeoutMilliseconds` is a wall-clock bound on the whole screening pass. **Do not remove it.** Before any budget existed, some servers answered in 20 seconds to a minute per request; multiplied by the per-probe request count, one pathological server could stall a run for many minutes. A hard ceiling on choosing a server is a requirement, not an optimisation.

Two properties make the ceiling affordable where the old per-candidate budget was not:

- **Concurrency.** Candidates are screened at the same time, so the worst case is about one timeout rather than one per server. The time taken to choose a server does not grow with the length of the server list, which is why nothing has to cap how many servers discovery returns.
- **Stragglers are abandoned, not awaited.** At the ceiling, screening takes whatever answered and moves on. Waiting for an outstanding request would make the ceiling advisory, and a handler that ignores cancellation would make it meaningless.

`ScreeningRequestCount` sits beside the ceiling and sets how many requests each server is sent. The screening figure is the **fastest** of the requests that completed, not their average: screening has no warm-up, so the first request carries connection setup the link itself is not responsible for.

## Once chosen, the server does not change

If the winner's full measurement fails, the run fails. It does not fall back to the next-ranked candidate. Latency, download, upload and any future test all run against the one server screening chose, so a reported result always describes a single server rather than a blend of two.

## `servers -l` lists what selection would rank

`ListServersCommand` screens each server through `GetFastestServerByLatencyAsync`, one candidate at a time, so the latency it lists is the figure auto-selection ranks on. A server listed with a latency is a server a real run would consider; a server listed with `-` could not be reached, and a run would not have used it either. That equivalence is the point — the table and the run used to disagree because they probed differently.

Rows keep their discovered order and are not re-sorted by latency: "ranked" means the user can see and compare the figures, not that the table reorders itself.

`servers -f` routes through the same selection path and so stays consistent for free. It reports the screening figure, as the `-l` table does.

## What is deliberately not here

- **Per-server diagnostics.** Why an individual server failed to answer is not reported. `No servers available` stays as it is.
- **A dedicated exception type.** The "nothing answered" case still throws a bare `Exception` carrying that message, because the user-visible message and exit code must not change.
- **A cap on discovered servers.** Unnecessary while screening cost is bounded by the ceiling rather than by candidate count.

---

**Last Updated**: October 2026
