# Asking context-mode for Its Own Counters

**Intent:** Make `plugin-report.sh`'s PERFORMANCE section report context-mode's usage counters itself, the way it already does for rtk and read-once, instead of printing an instruction to go and run `ctx_stats` in a Claude session and join the answer back to a report that has already scrolled past.

**Behaviour:** `scripts/plugin-report.sh`, whose call-site comments carry why the per-PID `stats-pid-*.json` sidecars are not the answer, why the probe is confined, and the two load-bearing `claude -p` argument details.

**Decisions:**

- *Rejected — reading the authoritative SQLite store directly.* `ctx_stats` and context-mode's own statusline both read its SessionDB, so reading it ourselves would be correct today. It loses on what it binds us to: the `sqlite3` binary would have to be present on every box, and the script would depend on an undocumented private schema that breaks silently when it changes — a strictly worse dependency than the documented-legacy JSON the change had just abandoned, and the same class of fault in a new place.
- *Rejected — `context-mode statusline`.* A supported CLI surface, but it carries no figures by design, it drains stdin (hanging the report on a terminal), and on *every* internal failure it prints a hardcoded `saves ~98% of context window` with a green status dot while exiting 0. A broken context-mode would have rendered as a healthy-looking 98% under a heading reading "Live counters" — worse than a fabricated zero.
- *Author override, recorded because it departs from the issue.* Issue #262 made an opt-out flag a condition of starting a model from this script. The author chose always-on with no flag: the header discloses the cost, and a switch on a rarely-run manual report is machinery without a reader.

**Date:** 2026-09-05
