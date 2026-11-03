# Live Displays Redraw On An Injected Switch

**Supersedes:** [2026-10-18-live-display-tests-snapshot-the-final-screen](2026-10-18-live-display-tests-snapshot-the-final-screen.md) — "instant stubs, so the capture holds one frame per progress display". They do not guarantee it; a loaded machine still crossed the refresh interval.

**Intent:** Make the captured screen a function of what was measured, not of how fast the machine ran.

**Behaviour:** Issue #490.

**Constraints:**

- Spectre.Console 0.57.1 redraws a progress display from a thread of its own, started by `AutoRefresh`, and takes no `TimeProvider` — so the issue's preferred seam, a time provider the fixture owns, cannot reach it.
- A spinner frame is picked from the `deltaTime` the renderer measures, so one redraw too many changes the screen.
- Production must go on redrawing mid-measurement.

**Decisions:**

- `ILiveDisplayRefresh` carries whether a display redraws itself, injected as `IClock` and `IWaiter` are, and registered non-refreshing in the test host beside the other stubs that would make a snapshot machine-specific.
- Injected into `DefaultConsoleWriter`'s constructor. Rejected: a ninth parameter on `IConsoleWriter.PerformSpeedTestAsync`, handing the switch to three writers that draw nothing.
- Rejected: a very long `RefreshRate`. It starves the refresh thread rather than saying there is no clock.
- `ListServersCommand`'s live table is left alone: no test renders it interactively, so it has no flake to fix.

**Date:** 2026-11-03
