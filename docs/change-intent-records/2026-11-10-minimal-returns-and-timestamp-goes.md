# Minimal Output Returns And The Timestamp Switch Goes

**Intent:** Give `--loop` and `--count` back the one-line-per-run format they were made for, and settle the timestamp per format rather than behind a switch.

**Behaviour:** See issue #540 and its confirmed decisions.

**Supersedes:** [`2026-11-09-simple-replaces-minimal`](2026-11-09-simple-replaces-minimal.md), which rejected a returning `--minimal` because its fields would shift under existing logs. That no longer outweighs the job: the line is the one `--minimal --timestamp` already wrote, so only scripts that never passed `-t` see a field move, and issue #540 states that break under the pre-1.0 stance.

**Constraints:**

- `--minimal` and `--simple` alone turn the probes off, in `Program.BuildSpeedTestSettings`; a new format inherits probes-on unasked.
- Both render a figure through one expression on `SpeedTestResultExtensions`.

**Decisions:**

- The time is always on the `--minimal` line. Rejected: keeping `--timestamp`, which would then shape only the format that always wants it.
- `--timestamp` gets the parser's unrecognised-option error. Rejected: a hidden removal notice, since no other switch took the time over.
- Default output carries no timestamp. Rejected: always showing one, since it is read live with the clock on screen.

**Date:** 2026-11-10
