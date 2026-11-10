# Minimal Output Returns And The Timestamp Switch Goes

**Intent:** Give `--loop` and `--count` back the one-line-per-run format they were made for, and settle the timestamp per format rather than behind a switch.

**Behaviour:** See issue #540 and its confirmed decisions.

**Supersedes:** [`2026-11-09-simple-replaces-minimal`](2026-11-09-simple-replaces-minimal.md) — `--minimal` is a format again rather than a switch that fails, and `--simple` no longer carries a labelled `Timestamp` line. The reasoning for giving `speedtest-cli` migrants `--simple`'s name and shape stands.

**Constraints:**

- Four formats now share the one-of-these rule and the probes-off decision in `Program.BuildOoklaSettings`; a fifth inherits probes-on unasked.
- `--minimal` and `--simple` must render the same measurement identically, so the figure is one expression on `SpeedTestResultExtensions`.

**Decisions:**

- The time is always on the `--minimal` line. Rejected: keeping `--timestamp` to turn it on — a line in a scrolling log is of little use without it, and the switch then shaped only the one format that always wants it.
- `--timestamp` is removed outright, with the parser's own unrecognised-option error. Rejected: a hidden switch naming the removal, as `--verbosity` and `--server` have — there is nowhere honest to point, since the time did not move to another switch.
- Default output lost its timestamp with the switch. Rejected: making it unconditional there too — it is read live, where the clock is already on screen.

**Date:** 2026-11-10
