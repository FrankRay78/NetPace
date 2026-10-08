# A Format Selector Is Not A Verbosity Level

**Intent:** `--verbosity` presented a format choice as a level on a scale: `Minimal` selected a different writer, `Normal` was no choice, `Debug` added prose. One option could not carry terse output *and* extra detail together, which the `--diagnostics` work (#268) needs, so the switch was split first. Current behaviour is in USER_GUIDE ("Choosing an output format").

**Decisions:**

1. **Rejected: one `--format <value>` option.** Tidier, but it breaks `--csv` and `--json`, which work today, to solve a problem this change doesn't have. Chose three boolean peers, at most one selectable. `--json-pretty` shapes JSON rather than selecting a format, so `--json --json-pretty` stays valid.

2. **Rejected: keep `--verbosity` as a deprecated alias.** `--minimal` replaces only one of three former values; an alias would have to guess what `Normal` and `Debug` now mean. A switch that silently does something else is worse than one that stops.

3. **Rejected: delete the option and let the parser report it.** The generic unrecognised-argument message tells someone with `--verbosity` in a script or cron job nothing. Chose a hidden, still-registered option that rejects every invocation with one removal message (mechanism: comment in `Program.cs`).

4. **The error names no replacement.** `--minimal` is right only for someone who passed `Minimal`; `Normal` and `Debug` users would be sent somewhere that doesn't do what they asked. `--help` is the honest destination.

5. **Rejecting two format switches rides along deliberately.** Alone it wouldn't justify a break, but `--csv --json` silently picking one hides a mistyped flag indefinitely. The surface is already breaking, so this costs users one break instead of two.

6. **The Debug prose is removed, not relocated.** It is a weaker version of what #268 will provide, and keeping it on stdout would stop #268 from promising stdout is unaffected by diagnostics.

**Date:** 2026-10-07
