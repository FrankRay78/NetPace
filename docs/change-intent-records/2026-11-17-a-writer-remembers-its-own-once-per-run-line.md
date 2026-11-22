# A Writer Remembers Its Own Once-Per-Run Line

**Intent:** Stop a CSV run losing its column names when an early test fails, by making the header's position depend on what the run has written rather than on which attempt it is.

**Behaviour:** See issue #548. The header sits directly above the first data row the run writes; a run that writes no row writes no header.

**Constraints:**

- `IConsoleWriter.PerformSpeedTestAsync` loses its `bool initialSpeedTest` parameter, so a writer is no longer told where in the run it is. Four of the five writers ignored it.
- The writer the command builds serves one whole run, which is what makes per-run state on `CSVConsoleWriter` sound. Nothing enforces it: the one `new` beside the loop in `SpeedTestCommand` is the guarantee, and a cached or DI-registered writer would silently give its second run no header.
- `DefaultConsoleWriter` builds a fresh `SimpleConsoleWriter` per test on the cannot-animate path, so the one-instance-per-run property holds for the writer the command selects, not for every writer reached.

**Decisions:**

- Passing the test index and letting the writer compare it. Rejected: it still keys the header off the attempt, which is the fault being fixed, and it tells every writer something only one of them uses.
- Having `SpeedTestCommand` track whether a row was written and keep passing a flag. Rejected: it puts the invariant where no caller can verify it, and makes the command know that CSV has a header.

**Date:** 2026-11-17
