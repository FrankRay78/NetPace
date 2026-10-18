# Live-display tests snapshot the final screen

**Intent:** Verify the live speed shown beside each progress bar without a timing-dependent test.

**Behaviour:** Issue #409 — a live speed figure beside each running test's progress bar, settling on the speed the result line reports.

**Constraints:**

- Spectre.Console's progress display redraws on a wall-clock refresh interval, not on each progress report, so how many frames a run produces depends on how long it takes.
- `TestConsole` emits no ANSI, so successive frames stack in the captured output with nothing reliable to split them on.
- Tests must be deterministic (Constitution, Testing Standards).

**Decisions:**

- The interactive test host (`CommandLineTestHost(interactive: true)`) runs against instant stubs, so the capture holds one frame — the final screen — and that is what the snapshots assert.
- "The figure changes as more data arrives" is verified one progress report at a time, against `LiveSpeedColumn`, rather than from the redraws. Rejected: spacing a stub's reports past the refresh interval to force a frame per report — it makes the frame count a function of machine speed.
- Rejected: forcing a redraw per progress report in the writer. It would make the frames deterministic, but at the cost of stacking every frame into the snapshot of every interactive run, and it ties the display's refresh cadence to the measurement's report rate for a test's benefit.
- `ScriptedSpeedTester.ReportsProgress` defaults off so the fixture's existing non-interactive snapshots stay byte-identical, which is what makes "output outside a terminal is unchanged" checkable across the suite.

**Date:** 2026-10-18
