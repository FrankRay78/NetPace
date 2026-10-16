# Machine-readable output holds data, not presentation

**Intent:** Settle one rule for both machine-readable formats: CSV and JSON report measurements, and nothing on the command line reshapes them. A script reads one field per measurement, in a fixed unit, and gets the same bytes whatever unit switches the run carried.

**Behaviour:** Specified by issue #389, whose acceptance criteria this implements: JSON stops writing the `Latency`, `DownloadSpeed` and `UploadSpeed` display strings and keeps only `LatencyMilliseconds`, `DownloadSpeedBitsPerSecond` and `UploadSpeedBitsPerSecond`, and `--unit`, `--unit-scale` and `--unit-system` shape the default and minimal output only.

**Constraints:**

- Pre-1.0, so removing three fields a script may read needs no deprecation period — but the replacement has to already be there, which it is: the numbers landed in [`2026-10-15-json-numbers-name-their-unit-in-the-field`](2026-10-15-json-numbers-name-their-unit-in-the-field.md) and carry the same measurements.
- The unit switches stay accepted with `--csv`, `--json` and `--json-pretty`. A script that swaps `--minimal` for `--json` keeps its switches on the command line and must not start failing.
- `NetPace.Core` is untouched. Its results already hold raw measurements and `GetSpeedString` stays available to library consumers; only the console's JSON writer stops calling it.

**Decisions:**

- **One rule for both machine-readable formats, derived from CSV.** Rejected: leaving JSON carrying both forms, which is the state the numbers landed in. It made each future measurement cost two fields and a paragraph of documentation explaining which switches reach which one, and it left the plainest field names — `DownloadSpeed`, `Latency` — holding the display text so the number had to carry the longer name. The earlier record's constraint that every existing field survive untouched was the right call *then*, when CSV had not yet been settled and the numbers were new; with CSV numbers-only, keeping JSON's strings would have made the two formats follow different rules.
- **No replacement for the display strings in JSON.** Rejected: a formatted block, or an opt-in switch restoring them. `--minimal` already writes the line someone pastes into a notification, and it follows the unit switches. A second way to get the same text is a second thing to keep consistent.
- **The unit switches are accepted and silent with a machine-readable format, not refused.** Rejected: an error or a warning naming the switch as inapplicable. A script that changes format should not have to change its flags too, and a warning on stderr is noise a cron job collects forever. The `--help` text for each switch states its scope instead, so the user learns it where they choose the switch.
- **Narrow the consistency rule rather than drop it.** The constitution's *Units and Formatting* consistency rule now names default and minimal, and the fixed-unit rule covers CSV and JSON together, which must agree with each other. Rejected: deleting the consistency rule as vacuous with two modes left. Two modes can still drift, and the rule is what catches it.

**Date:** 2026-10-16
