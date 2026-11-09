# Simple Output Replaces Minimal

**Intent:** Give someone leaving `speedtest-cli` the switch name and line shape their scripts already parse, without a rename that would quietly change what those scripts read.

**Behaviour:** See issue #525 and its confirmed decisions.

**Supersedes:** [`2026-10-07-minimal-replaces-verbosity`](2026-10-07-minimal-replaces-verbosity.md) — the switch that replaced `--verbosity Minimal` is now `--simple`, and its one line is one line per figure. The reasoning for retiring `--verbosity` stands.

**Constraints:** The line shape is a contract: `<name>: <value>`, one figure per line, the timestamp labelled like the rest. A bool-typed option carries an implicit parse result, so the retired `--minimal` has to be a string option or its validator fires on every run.

**Decisions:**

- Name and shape changed together. Rejected: renaming alone — the old single line feeds `grep Download | awk '{print $2}'` the latency as if it were the download speed, silently.
- `--minimal` fails outright. Rejected: keeping it as an alias — the shape behind it changed, so an alias would be the silent-wrong-logs case under a different spelling.
- Units stay auto-scaled. Rejected: `speedtest-cli`'s fixed `Mbit/s` — the rest of NetPace auto-scales, and `--unit-scale` already pins a unit.

**Date:** 2026-11-09
