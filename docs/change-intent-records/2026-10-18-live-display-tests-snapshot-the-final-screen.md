# Live-display tests snapshot the final screen

**Intent:** Verify the live speed shown beside each progress bar without a timing-dependent test.

**Behaviour:** Issue #409 — a live speed figure beside each running test's progress bar, settling on the speed the result line reports.

**Constraints:**

- Spectre.Console's progress display redraws on a wall-clock refresh interval, not on each progress report, so how many frames a run produces depends on how long it takes.
- `TestConsole` emits no ANSI, so successive frames stack in the captured output with nothing reliable to split them on.
- Tests must be deterministic (Constitution, Testing Standards).

**Decisions:**

- The interactive test host (`GetInteractiveCommandLineTestHost`, which pairs `interactive: true` with a finite width) runs against instant stubs, so the capture holds one frame per progress display — the final screen of each — and that is what the snapshots assert.
- A final frame cannot distinguish the figure the last progress report produced from the one the result settled, unless the two differ. `SpeedTestProgressClimb` therefore stops its last report short of the result's own byte count, so a run that never settles the figure shows a different number from the result line and the snapshots catch it.
- Known gap: nothing observes that the writer reports *during* a run. The column is constructed inside the writer, and a final-frame capture cannot show a mid-run figure, so the writer could stop feeding the column between the first report and completion with the suite still green. `LiveSpeedColumnTests` covers the climb directly instead. Closing the gap needs the column injected into the writer, which buys a test the shape of the production code — rejected on the same grounds as the frame-count options below.
- "The figure changes as more data arrives" is verified one progress report at a time, against `LiveSpeedColumn`, rather than from the redraws. Rejected: spacing a stub's reports past the refresh interval to force a frame per report — it makes the frame count a function of machine speed.
- Rejected: forcing a redraw per progress report in the writer. It would make the frames deterministic, but at the cost of stacking every frame into the snapshot of every interactive run, and it ties the display's refresh cadence to the measurement's report rate for a test's benefit.
- `ScriptedSpeedTester.ReportsProgress` defaults off so the fixture's existing non-interactive snapshots stay byte-identical, which is what makes "output outside a terminal is unchanged" checkable across the suite.

**Date:** 2026-10-18
