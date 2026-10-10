# Diagnostic record set and the two clocks

**Intent:** Build `--diagnostics` ([#268](https://github.com/FrankRay78/NetPace/issues/268)) — a per-request diagnostic stream in every output format.

**Behaviour:** Specified by #268's acceptance criteria, its Confirmed decisions, and the record-format decision comment. `USER_GUIDE.md` → *Diagnosing a failure with `--diagnostics`* documents the result.

**Constraints:**
- The result stream must be byte-identical with and without the switch, in every format.
- Snapshots need a fixed clock and fixed environment values, or they become machine-specific.

**Decisions** (only the ones #268 does not already settle):
- **Two clocks.** `IDiagnosticClock` is separate from `IClock` rather than reusing it, because the test clock stubs count the reads they are given — one shared instance would make the presence of `--diagnostics` shift the timestamp in the *result*, breaking the byte-identical criterion. Request records bypass both: they carry the time `NetPace.Core` stamped (`IDiagnosticRecorder.RecordAt`).
- **`test.end` counts are derived from the request records, not from `SpeedTestResult`.** The summary then cannot disagree with the records above it, and it can report `cancelled=`, which `SpeedTestResult` does not carry. Rejected: adding `RequestsCancelled` to `SpeedTestResult`, which is a `required`-property record — every construction site in the suite would have had to change for a field no output format shows.
- **`duration_ms` dropped from `test.end`** (the format sketch shows it). The only deterministic source is `SpeedTestResult.ElapsedMilliseconds`, which the latency and screening tests have no equivalent of; console wall-clock time is not snapshot-stable. Each `request` record carries its own `duration_ms`, and the `test.start`/`test.end` timestamps bracket the test.
- **`server.selected` carries `sponsor`, `location` and `url`** rather than the sketch's `id` and `host` — `IServer` has no server id and no host:port separate from the URL.
- **`latency_ms` on `server.selected` appears only on the `auto-latency` route.** The field is the figure the *choice* was based on; `specified` and `first-in-list` chose on something other than latency, so there is no such figure to report. The measured figure is in the latency test's own records.
- **The upload redirect probe may issue several requests** (one per hop), each recorded; the measured uploads are numbered after them, so sequence numbers stay distinct within the test.
- **`SpeedTestStub` reports no per-request diagnostics.** It fabricates aggregate results with no requests behind them, so any records it invented would contradict its own counts. `NetPace.Console.Tests/DiagnosticSpeedTester.cs` is the double for diagnostic snapshots: a handful of requests per test, with results that are exactly what those records add up to.

**Date:** 2026-10-09
