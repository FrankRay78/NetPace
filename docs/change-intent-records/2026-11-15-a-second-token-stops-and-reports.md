# A Second Token Stops A Measurement And Keeps Its Figure

**Intent:** Let a caller bound how long a speed test takes and still be given the figure measured over the time it ran, so a run cut short is not a failure.

**Behaviour:** See issue #555, whose confirmed decisions chose this mechanism.

**Constraints:** `ISpeedTestService` ships to NuGet, so this breaks its implementers: the richest latency, download and upload overloads take a trailing `CancellationToken stopAndReportToken`. Callers recompile unchanged, the parameter being optional and last, but an assembly built against the old signatures must be rebuilt. No time limit is offered to library consumers, who bound their own calls.

**Decisions:**

- A second token. Rejected: reusing `cancellationToken` with a flag on the result, which would stop a caller telling its own Ctrl-C from a stop, and Ctrl-C must still produce nothing.
- The latency call takes it too, though the issue named only download and upload: reporting the probes already answered cannot be done from outside the probe loop.
- A latency stop with no probe answered raises `OperationCanceledException`. Rejected: a zeroed `LatencyTestResult`, which reads as a perfect line.
- The library records a generic stop reason, which the console's diagnostics writer replaces. Rejected: naming the time limit in the library, which knows nothing of why its caller stopped.

**Date:** 2026-11-15
