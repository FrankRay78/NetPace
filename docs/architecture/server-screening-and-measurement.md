# Server Screening and Measurement

> How NetPace chooses a speed test server, why choosing and measuring are two different jobs, and why the ceiling on choosing must stay.

**Scope**: `OoklaSpeedtest.GetFastestServerByLatencyAsync`, `ServerDiscoverySettings`, `ServerSelector`, `ListServersCommand`
**Audience**: anyone changing server selection, the latency probe, or the settings that bound either
**Decision record**: [screening-separate-from-measuring](../change-intent-records/2026-10-06-screening-separate-from-measuring.md)

---

## The split

Choosing a server and measuring a server are two different jobs with two different accuracy requirements, and NetPace treats them that way.

**Screening** answers "is this server reachable, and roughly how fast?" for every discovered candidate. It is a ranking pass: a few requests per server with no deliberate waiting between them, run concurrently across all candidates, under one overall ceiling. It is cheap on purpose, because it runs against every server on the list.

**Measuring** answers "what is the latency of this server, and how steady is it?" to the accuracy NetPace reports. It is the probe NetPace has always had — a fixed number of requests spaced by a fixed interval, averaged — and it runs exactly once, on the winner. Each of its requests has `LatencyTestSettings.HttpTimeoutMilliseconds` to answer, ten seconds by default so that a slow link is measured rather than refused; a probe that misses ends the measurement rather than being averaged out of it, since a link that drops a reply has not been measured. It also produces the lowest and highest probe times, and **jitter**, on the result it returns: the mean of the absolute differences between consecutive probe times, which is unevenness as the change from one reply to the next rather than the spread of the probes around their average. Jitter is on the live table default output draws at a terminal, beside the latency it belongs to; the lowest and highest reach library consumers only. Screening produces none of it, and that is the clearest statement of the split — screening times each of its requests but keeps only the fastest, so no run of probe times survives to derive a jitter figure from, and it reports zero, with the lowest and highest probe times stated as not recorded. A download or an upload produces jitter too, from its own probes, under the same definition so that a loaded figure stays comparable with an idle one; that is a third measurement rather than part of choosing a server, and it is not in this document's scope.

`GetFastestServerByLatencyAsync` is the screening pass and returns the winner carrying its *screening* latency, and no jitter. The console then measures that server with `GetServerLatencyAsync`, and the measured figure is the one reported. The interface did not change: screening is a reinterpretation of what "find the fastest server" already meant, not a new method.

`ServerSelector` takes the same two steps on the console side, in that order and for every output format: `ChooseServerAsync` settles the server and writes the `server.selected` diagnostic record there and then, and `MeasureLatencyAsync` follows. The record lands when the choice is made rather than when the measurement returns, so default output can name the server while its first probe is still out — on a slow link that is seconds of otherwise blank screen — and a diagnostic log names the server even for a run whose measurement then failed.

## Screening ranks, it does not reject

A server is **reachable** when at least one of its screening requests completes before the pass ends. The pass ends at the ceiling at the latest, and sooner once another candidate has been ranked by the end of the first stage (*The ceiling is spent in stages, not waited out*, below). A reachable but slow server is a valid candidate that simply loses. Only a server that produced no completed request at all drops out. That includes an entry whose address is not an `http` or `https` URL: the list comes from a remote feed, so an entry NetPace cannot request is ranked out like any other unreachable server rather than allowed to fail the whole selection.

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

Abandoned is not forgotten, though: an abandoned candidate contributes no result and reports no progress once the pass is over, and whatever it throws on its way out is observed rather than left to surface as an unobserved task exception. A fault raised by a candidate that finished inside the ceiling is reported to the caller, because a screening that throws something other than "unreachable" is a defect in NetPace rather than a verdict on a server. A fault from a candidate abandoned at the ceiling can only be observed and discarded — by then nothing is waiting on it, which is the price of keeping the ceiling hard.

A pass that ranked nothing throws `NoServersAvailableException`, carrying the message the console prints. The type is public so that this one outcome — a network condition, not a fault — can be caught on its own, by NetPace's own commands and by a library consumer alike; a consumer catching it is reading a verdict about the network, not handling a defect.

`ScreeningRequestCount` sits beside the ceiling and sets how many requests each server is sent. The screening figure is the **fastest** of the requests that completed, not their average: screening has no warm-up, so the first request carries connection setup the link itself is not responsible for.

The figure and the ceiling read different clocks, deliberately. How long a screening request took — like how long a latency probe or a transfer took — is measured on the time provider the caller passed to `OoklaSpeedtest`. Candidates are screened concurrently against that one clock, so a caller reading an exact figure off a clock it moves itself has to let one candidate finish before the next is answered; otherwise an advance made for one lands inside another's measurement. The ceiling above is not a measurement but a bound on how long to wait, and stays on the system clock along with the per-request HTTP timeouts; a controlled clock that stood still would otherwise suspend them.

## The ceiling is spent in stages, not waited out

A link whose round trip is a second or two — a geostationary satellite, a congested mobile cell, 2G data — often cannot answer a screening request inside two seconds, and those are the links a speed test matters most on. So the ceiling is ten seconds. Raising it and leaving it at that would have cost every run with one silent candidate eight seconds it never used to spend: a pass ends early only once *every* candidate has finished, so the ceiling is what ends a pass still holding a straggler, however quick the candidates that answered were.

The ceiling is therefore spent in two stages. The first is `ServerDiscoverySettings.FirstScreeningStageMilliseconds`, two seconds by default, which was the whole ceiling before. A pass that has ranked a candidate by the end of it takes the winner and moves on. Otherwise the second stage waits for every candidate to finish, or for the rest of the ceiling, and not merely for the first candidate ranked: ending there would compare one server again, the illusion described under *Screening ranks, it does not reject*. A candidate is ranked only once it has finished all `ScreeningRequestCount` of its requests, so what the first stage leaves untouched is a run whose candidates get through that many round trips inside it; a slower link that the old two-second ceiling would have ranked on a partial figure now waits out the rest of the new one instead. Only a pass that has ranked nothing by then — the run that would otherwise have ended in `No servers available` — waits out the rest, which is what gives a slow link the time to answer at all. A pass whose candidates have all already failed on their own merits ends there and then, so a candidate that is refused, or that answers with something that is not a speed test server, costs the pass nothing; one that simply never replies cannot be told from a slow one, and costs the whole ceiling.

A single-candidate pass has no sibling to rank inside the first stage, so whenever its one candidate is unreachable it spends the whole ceiling before giving up. That is how `servers -l` screens each row, so an unreachable server there now costs ten seconds where it used to cost two; the rows are screened together, so the listing as a whole still takes about one ceiling. `servers -f`, like a run, screens every server in one pass, and spends the whole ceiling only when nothing has been ranked by the end of the first stage.

The ceiling now bounds how long a pass that has ranked nothing waits, not how long every candidate is given: once a quicker candidate is ranked inside the first stage, the slower ones are abandoned there. A consumer that set the ceiling above two seconds so that slower servers could compete gets that back by setting the first stage to the ceiling too, since a first stage at or above the ceiling spends it in a single stage. A consumer whose candidates need longer than two seconds to be ranked — more screening requests than the default, on a link slow enough that they do not all return inside the first stage — raises the first stage to suit.

The first stage is a setting rather than a constant for those two consumers: when the ceiling became the patience of a pass that has ranked nothing, rather than the patience given every candidate, the callers who relied on the old meaning needed a way back. The default leaves every other caller choosing a server exactly as fast as before. Rationale: CIR [`2026-11-09-screening-ceiling-is-spent-in-stages`](../change-intent-records/2026-11-09-screening-ceiling-is-spent-in-stages.md).

## Once chosen, the server does not change

If the winner's full measurement fails, the run fails. It does not fall back to the next-ranked candidate. Latency, download, upload and any future test all run against the one server screening chose, so a reported result always describes a single server rather than a blend of two.

## `servers -l` lists what selection would rank

`ListServersCommand` screens each server through `GetFastestServerByLatencyAsync`, one candidate per call so that every server gets a figure rather than only the winner, and with all the calls in flight together so the listing takes about one ceiling rather than one per unreachable server. The latency it lists is the screening figure auto-selection ranks on, taken the same way. What a one-candidate pass cannot reproduce is the first stage: it has no sibling to rank inside it, so every row is given the whole ceiling, where a run stops waiting at the end of the first stage — two seconds by default — once any candidate is ranked. A server whose screening needs longer than that can therefore be listed with a latency — even the lowest in the table — and still be one a run with a quicker server beside it would have abandoned. That mismatch is accepted: the listing exists to show every server a figure, and cutting each row at two seconds would list a slow link as unreachable. A server listed with `-` could not be reached inside the whole ceiling, and a run would not have used it either. A row left blank is neither: the user cancelled before that server answered, so the listing never reached a verdict on it. The table and the run used to disagree because they probed differently; they now probe the same way, and differ only in how long they wait.

Rows keep their discovered order and are not re-sorted by latency, though they fill in as each server answers rather than top to bottom: "ranked" means the user can see and compare the figures, not that the table reorders itself.

`servers -f` routes through the same selection path and so stays consistent for free. It reports the screening figure, as the `-l` table does.

## What is deliberately not here

- **Per-server diagnostics.** Why an individual server failed to answer is not reported. `No servers available` stays as the message it has always been.
- **A cap on discovered servers.** Unnecessary while screening cost is bounded by the ceiling rather than by candidate count.

---

**Last Updated**: November 2026
