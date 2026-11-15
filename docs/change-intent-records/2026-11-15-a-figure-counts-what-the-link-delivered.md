# A Figure Counts What The Link Delivered

**Intent:** Report a link at what it carried, rather than at what the requests that happened to finish carried.

**Behaviour:** See issue [#534](https://github.com/FrankRay78/NetPace/issues/534) and its confirmed decisions.

**Constraints:**

- `SpeedTestResult.BytesProcessed` is a published `NetPace.Core` member and its meaning changes: it now includes the bytes of requests that failed or were dropped. `RequestsSucceeded`, `RequestsFailed` and `--fail-on` are untouched.
- OoklaServer closes a connection it has held longer than the undocumented `OoklaServer.maxConnectionSeconds`. Public servers run the stock value, so it is a condition to measure under rather than a fault to fix; `docker/ooklaserver/README.md` records it.

**Decisions:**

- A transfer keeps two totals: delivered bytes for the figure, successfully-returned bytes for the byte budget. Rejected: one total, with the budget spent on delivered bytes, which is arguably truer to what a budget means but trips the cap sooner and so moves the succeeded and failed counts this change holds fixed.
- An upload counts what was written to the transport. Rejected: counting only what the server acknowledged, which a client cannot see; the over-count is one buffer.
- The live figure is paced rather than reported per chunk. Rejected: a report per chunk, which redraws a consumer thousands of times a second on a fast link.

**Date:** 2026-11-15
