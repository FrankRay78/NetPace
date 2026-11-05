# Jitter Is The Mean Change Between Consecutive Probes

**Intent:** Settle what "jitter" means in NetPace once, so the idle figure and the loaded figure that follows it stay comparable.

**Behaviour:** See issue #454 — a latency measurement reports jitter in whole milliseconds beside the latency.

**Constraints:**

- Every member of `LatencyTestResult` is `required`, so adding one breaks a consumer that constructs a result themselves. Pre-1.0, no deprecation period applies.
- A jitter figure needs a run of probe times. Screening times each of its requests but keeps only the fastest, so a screened winner reports zero and only measuring produces a figure — which is the split in [server screening and measurement](../architecture/server-screening-and-measurement.md) restated.

**Decisions:**

- The mean of the absolute differences between consecutive probes, RFC 3550's interarrival-jitter family. Rejected: the standard deviation of the samples — it measures the spread around an average rather than the change from one reply to the next, which is what a caller on the line hears.
- Rounded to the nearest whole millisecond, with a half rounding up. Rejected: truncating, which reports half a millisecond of real unevenness as perfectly steady.

**Date:** 2026-11-01
