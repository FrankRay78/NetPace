# Asking context-mode for Its Own Counters

**Intent:** Make `plugin-report.sh`'s PERFORMANCE section report context-mode's usage counters itself, the way it already does for rtk and read-once, instead of printing an instruction to go and run `ctx_stats` in a Claude session and mentally join the answer back to a report that has already scrolled past.

**Behaviour:**

- Given context-mode is installed and `claude` is available, When the report runs, Then the context-mode row carries the box-wide savings and spend figures that `ctx_stats` itself reports.
- Given the counters probe fails, returns nothing, or `claude` is not on PATH, When the report runs, Then the row reports an unreadable probe — never a zero, and never a figure.
- Given context-mode is installed, When the report runs, Then the TOOLING `reachable=` column reflects `context-mode doctor`'s own verdict, and any exit that is not that verdict reads `unknown`.
- Given the report runs, When a reader reads the header, Then what the script spends, writes and reaches over the network is stated there in full.

**Constraints:**

- "I could not look" is not "no". Every probe that cannot reach a verdict reports `unknown`. A counter row is the easiest place in the script to break this, because a failed read and a genuine zero look identical once printed.
- TOOLING / CONFIG / HOOKS must stay free of timestamps and machine-specific paths so two boxes diff cleanly. PERFORMANCE is exempt, which is why the figures live only there.
- No new persistent state of our own. The report must be a pure function of what it can observe at the moment it runs — nothing cached, accumulated, or carried between runs for us to maintain or migrate later.

**Decisions:**

*The premise this change started from was false, and that is the whole story.* Issue #262 established that context-mode writes per-PID `stats-pid-*.json` sidecars under its storage root, and recommended simply reading them — instant, no subprocess, no spend. That was implemented first. It was wrong: context-mode's own `bin/statusline.mjs` records those sidecars as legacy and "no longer the source of truth" (they were eventually-consistent and PID-scoped), and on a live box they disagree with `ctx_stats` by a wide margin — the sidecars reported $2.01 across 5 sessions where `ctx_stats` reported $3.65 across 33 conversations and 10 projects. A `schemaVersion` guard does not catch this, because the risk is not shape drift but source drift: a frozen legacy store passes the guard and prints confident, wrong figures forever. Reading them would have reproduced, in a new place, exactly the fault the issue was raised to fix.

*Rejected — read the authoritative SQLite store directly.* `ctx_stats` and the statusline both read context-mode's SessionDB. Reading it ourselves would need the `sqlite3` binary present on every box and would bind this script to an undocumented private schema — a strictly worse dependency than the documented-legacy JSON we had just abandoned, and one that breaks silently when it changes.

*Rejected — `context-mode statusline`.* A supported CLI surface, but it carries no figures by design, it drains stdin (hanging the report on a terminal), and it prints a hardcoded `saves ~98% of context window` with a green status dot on *every* internal failure while exiting 0. A broken context-mode would have rendered as a healthy-looking 98% under a heading reading "Live counters" — worse than a fabricated zero.

*Chosen — ask context-mode, by starting a headless `claude -p` with only the `ctx_stats` tool allowed.* It is the sole route to figures that are correct by construction, since it is the same MCP handler the tool answers with itself. Issue #262 raised and rejected this option as "a large price for formatting"; that reasoning does not survive the discovery above, because it is no longer formatting — it is the only correct answer available. Two argument details are load-bearing and are recorded at the call site: the prompt must be the positional immediately after `-p` (`--disallowedTools` is variadic and eats a following positional), and stdin must be redirected or the call stalls on the terminal. The reply is reported whole and the probe runs in the repo the report describes; the scope reasoning for that is below.

*Consequence accepted — the script is no longer inert, and the header says so instead of hedging.* It now starts a model, which costs seconds and money and needs the network and a logged-in CLI; `context-mode doctor` additionally checks the npm registry; and context-mode's own CLI creates its empty storage directories when absent. The previous header claimed "installs nothing, edits nothing, starts nothing", and an earlier draft of this change replaced that with "makes no network call" — which was false the moment `doctor` was added as a probe. A header block that people rely on before running a script on an unfamiliar box has to be right, so it now states the full cost.

*Author override, recorded because it departs from the issue.* Issue #262 made an opt-out flag (a `--no-ai`, as its sibling script has) a condition of taking this route. The author chose always-on with no flag: the header discloses the cost, and a switch on a rarely-run manual report is machinery without a reader. Noted here so the departure is visible rather than lost.

*The reply is reported whole, and the probe runs in the repo the report is about.* An earlier revision sliced it down to the one line proven to be box-wide, and threw away the headline totals with it — the cure was worse than the disease, and slicing was what caused it. What is worth knowing, and was measured rather than assumed: the reply is scoped to the directory the probe runs in. The same probe run from this repo and from the Claude home returns an identical section-3 "All your work" total but a *different* section-4 dollar figure ($3.84 against $2.80, each stable on repeat). Running it in the repo is therefore the scope a per-repo tooling report wants — section 1 carries this repo's real figures rather than an empty throwaway session, and no phantom project is created (a fresh directory per run adds one every time, observed pushing the project count from 10 to 12 during development). The row is labelled as a headless probe so "this chat" in the reply is read as the probe's session, which is what it is.

*The probe is confined, and runs from a fixed directory.* `--allowedTools` is a permission grant, not a whitelist: `Read`, `Glob`, `Grep` and `Task` remain auto-approved unless denied, so a model asked for context-mode's counters with `ctx_stats` unavailable would go looking — and find the legacy sidecars this change exists to stop trusting. They are denied explicitly. Running in the repo means the nested session also runs this repo's hooks, which is the accepted cost of the scope decision above. A blocking `Stop` hook forces another turn, so the genuine counters can arrive with the model's answer to that hook appended after them — both section markers still present, so a substring check passes. The reply is therefore also required to *end* where `ctx_stats` ends, at its bare version-line footer, which is what detects trailing text. Should that footer ever change, the row reports `unknown` and shows the reply: a visible failure rather than a silently wrong figure.

*An unrecognised reply is never printed as a reading.* `claude -p` does not guarantee a verbatim echo — it may paraphrase, refuse, report a usage limit, or be truncated, and every one of those exits 0. The reply is accepted as counters only when both section delimiters are present; otherwise the row reports `unknown` and shows the reply under a label saying it is not a counter reading.

*Reading is still stateless.* Nothing is cached or accumulated between runs. Each run asks and reports; delete everything and the next run asks again.

**Date:** 2026-09-05
