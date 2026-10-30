# CSV Is Always Comma-Separated, With Escaped Fields

**Intent:** Make every `--csv` record parse into the same columns as its header whatever the machine reports as its hostname or IP address, and stop carrying an option to vary the separator.

**Behaviour:** See [issue #460](https://github.com/FrankRay78/NetPace/issues/460).

**Constraints:** CSV is read by scripts, so output for values needing no escaping had to stay byte-for-byte as it was. The machine-readable timestamp contains `-`, `T`, `:` and `+`, which is why a configurable delimiter needed a validator of its own.

**Decisions:**

- One fixed comma with RFC 4180 quoting. Rejected: keeping `--csv-delimiter` and quoting as well — two mechanisms against the same risk, where every CSV consumer already reads commas.
- A line break inside a value written as a space. Rejected: quoting it, as RFC 4180 permits — the record would then span two lines, which `--csv` promises it never does and every line-oriented log reader assumes.
- Escaping applied to every field. Rejected: escaping only the two free-text fields — a free-text column added later would silently reintroduce the fault.

**Date:** 2026-10-29
