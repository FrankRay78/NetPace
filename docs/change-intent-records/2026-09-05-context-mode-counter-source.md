# Reading context-mode's Counters from Its On-Disk Store

**Intent:** Make `plugin-report.sh`'s PERFORMANCE section report context-mode's usage counters itself, the way it already does for rtk and read-once, instead of printing an instruction to go and run `ctx_stats` in a Claude session and mentally join the answer back to a report that has already scrolled past.

**Behaviour:**

- Given context-mode is installed and has recorded sessions, When the report runs, Then the context-mode row carries figures — recorded sessions, tool calls, and lifetime tokens and spend — comparable with the rows above it.
- Given context-mode is installed but has no counter store yet, When the report runs, Then the row carries context-mode's own one-line `statusline` summary, labelled as a summary rather than presented as counters.
- Given the store will not parse, or carries a `schemaVersion` this script was not written for, When the report runs, Then the row reports an unreadable probe — never a zero, and never the misread figure.
- Given context-mode is installed, When the report runs, Then the TOOLING `reachable=` column reflects whether `context-mode doctor` — the tool's own health check — passes, rather than a hardcoded `n/a`.

**Constraints:**

- The script's header contract: installs nothing, starts no model, makes no network call. It is advertised as inert to people asked to run it on an unfamiliar box, and that promise is the reason it gets run.
- "I could not look" is not "no". Every probe that cannot reach a verdict reports `unknown`. A counter row is the easiest place in the script to break this, because a failed read and a genuine zero look identical once printed.
- TOOLING / CONFIG / HOOKS must stay free of timestamps and machine-specific paths so two boxes diff cleanly. PERFORMANCE is exempt, which is why the new figures live only there.

**Decisions:**

Three sources for the figures were available, and the cheapest was chosen.

*Chosen — read the JSON store.* context-mode writes one `stats-pid-N.json` per session under the storage root `context-mode doctor` reports. Reading it is instant, needs no subprocess, no network and no spend, and it is the only option that yields figures comparable with the rtk and read-once rows. Its cost is a dependency on a private on-disk shape, paid down by checking every record's `schemaVersion` against the version this was written for: a bumped store reports `unknown` rather than being silently misread.

*Rejected — `context-mode statusline`.* A supported CLI surface, so it will not drift, but it yields one summary sentence and no token or spend figures. Kept as the fallback for when there is no store to read, where a sentence from the tool beats a fabricated zero.

*Rejected — spawn `claude -p` with `ctx_stats` allowed.* This returns the canonical, fully formatted output, and a sibling project already demonstrates a shell script folding a headless `claude -p` result back into its own. It was rejected because it costs seconds, real money and network access per run, and because starting a model would break the header's promise outright. That is a large price for formatting. Had it been chosen, the READ-ONLY paragraph would have had to be amended in the same change and the call made opt-out rather than on by default.

*Consequence accepted — the health probe is not purely inert.* `context-mode doctor` creates context-mode's empty storage directories under the Claude home when they are absent. This is the tool housekeeping its own state on a box where it is already installed, and it is the only write the script can cause; the header and `docs/agentic-workflow-NetPace.md` now say so rather than keeping an absolute "edits nothing" claim the code no longer honours.

*Trap noted in the code.* The plugin also ships a `stats.json` at its install root. It holds npm download counts for the README badge, not session counters, and it is the obvious wrong file to reach for here.

**Date:** 2026-09-05
