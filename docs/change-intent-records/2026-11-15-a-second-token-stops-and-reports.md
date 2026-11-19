# A Second Token Stops A Measurement And Keeps Its Figure

**Intent:** Let a caller bound how long a speed test takes and still be given the figure measured over the time it ran, so a run cut short is not a failure.

**Behaviour:** See issue #555, whose confirmed decisions chose this mechanism.

**Constraints:** This breaks `ISpeedTestService`'s implementers, since its richest latency, download and upload overloads gain a trailing optional `CancellationToken stopAndReportToken`. Callers must rebuild, and two kinds of call stop compiling: one in an expression tree, such as a Moq `Setup` (CS0854), and a method-group conversion to a delegate of the old shape (CS0123).

**Decisions:**

- A second token. Rejected: reusing `cancellationToken` with a flag on the result, which would stop a caller telling its own Ctrl-C from a stop, and Ctrl-C must still produce nothing.
- The latency call takes it too, though the issue named only download and upload: reporting the probes already answered cannot be done from outside the probe loop.
- A latency stop with no probe answered raises `OperationCanceledException`. Rejected: a zeroed `LatencyTestResult`, which reads as a perfect line.

**Date:** 2026-11-15
