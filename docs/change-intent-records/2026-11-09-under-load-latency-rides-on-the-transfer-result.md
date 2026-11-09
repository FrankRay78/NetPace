# Under-Load Latency Rides On The Transfer Result

**Intent:** Report the latency measured during a download or an upload without adding an interface method or a second measurement call.

**Behaviour:** See [issue #458](https://github.com/FrankRay78/NetPace/issues/458) and its confirmed decisions, which settled the figures, the settings and the log-line shape.

**Constraints:** `RequestKind` names what a request *was*, not whether its test was measuring it, so the console decides `event=probe` from the kind **and** the test. Nothing in `NetPace.Core` knows either log-line name.

**Decisions:**

- Probes share the transfer's `HttpClient`. Rejected: a second client. The handler leaves `MaxConnectionsPerServer` unbounded and HTTP/1.1 does not multiplex, so a probe already opens its own connection — a second client would only add proxy settings to keep in step.
- Probes carry their own run of `Sequence` numbers. Rejected: continuing the transfer's counter, which would gap the transfer's numbering by however many probes happened to go out.
- `LoadedLatencyIntervalMilliseconds` is rejected when non-positive even with probing switched off. Rejected: validating only when on, which leaves a stored interval that means nothing until someone switches probing back on and discovers it.

**Date:** 2026-11-09
