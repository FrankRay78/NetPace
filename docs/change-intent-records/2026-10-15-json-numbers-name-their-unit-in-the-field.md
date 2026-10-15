# JSON Numbers Name Their Unit In The Field Name

**Intent:** Give scripts a number they can compare, store and threshold without stripping a unit off a display string — and without the unit shifting between runs, which is what the default automatic scale does.

**Behaviour:** Specified by issue #371, whose acceptance criteria this implements: every JSON result carries the measured speeds and latency as plain numbers in a fixed unit beside the formatted values it already had.

**Constraints:**

- Today's JSON is a contract scripts already read. Every existing field and value had to survive untouched, so the numbers are additions, not replacements.
- CSV gains the same numbers later (consolidated backlog item 9). Whatever shape this took, that one has to match it, so the choice binds a change not yet written.

**Decisions:**

- Put the unit in the field name — `DownloadSpeedBitsPerSecond`, `LatencyMilliseconds`.
  - Rejected: separate unit fields (`DownloadSpeedUnit: "bps"`). Honest, and it generalises if the unit ever becomes configurable, but it doubles the field count and makes the common case — read one number — a two-field lookup. It also invites exactly the configurability the fixed-unit promise rules out.
  - Chose: the name, which is self-describing in a saved result with no extra fields and no lookup. Among comparable tools only fast-cli states its unit in the JSON at all; the rest leave it to their documentation, which a saved file does not carry.
- Report whole numbers, speed rounded to the nearest bit per second.
  - Rejected: decimals. Bits per second is already the finest unit in play, so the fraction carries nothing a consumer can use, and a float invites per-language formatting differences in output meant to be compared byte for byte.
  - Chose: whole numbers. Precision holds where it matters — the Tiny profile on a slow link measures in the Kbps range, which a whole bit count represents to five significant figures.
- Derive the number from one calculation shared with the formatting methods, exposed as `SpeedTestExtensions.GetBitsPerSecond`.
  - Rejected: computing bits per second in the JSON writer. It is three lines, and it is the third place in the solution that would hold the same arithmetic — the two that already existed drifting is the failure this guards against.
  - Chose: a public Core extension beside `GetSpeedString`, with the formatting path rewritten to use it. The rounding stays in the public method and the shared private helper returns the unrounded figure, so the formatted values are unchanged to the byte — a rounded figure fed back into byte-per-second formatting would have shifted them.

**Date:** 2026-10-15
