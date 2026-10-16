# The JSON timestamp is a DateTimeOffset, not a pre-formatted string

**Intent:** Write the CSV and JSON timestamp as ISO 8601 local time with its UTC offset (#387), so a machine-readable result names one unambiguous instant.

**Behaviour:** Specified by #387 — `2026-10-16T08:21:17+01:00` in both formats.

**Constraints:**

- `JsonResult` is serialised through a source-generated context, so the serialisation must stay trim/AOT-safe.
- System.Text.Json's default encoder escapes `+` inside a string value. A pre-formatted timestamp string therefore reaches the output as `"1980-01-01T10:05:00+01:00"` — correct to a JSON parser, unreadable to a person looking at `--json-pretty`.

**Decisions:**

- `JsonResult.Timestamp` is a `DateTimeOffset`. System.Text.Json writes one in ISO 8601 form with its offset and does not route it through the string encoder, so the `+` survives.
- Rejected: keeping the property a string and swapping in a relaxed `JavaScriptEncoder`. `JsonSourceGenerationOptions` has no encoder knob, so it would mean building `JsonSerializerOptions` around each context by hand — more moving parts than typing the property correctly, for the same output.
- System.Text.Json carries whatever sub-second precision the value has, while the CSV format string silently drops it. `Timestamps.ToWholeSecond` truncates on both paths so the two writers cannot disagree about the same instant.
- `IClock.Now` returns the offset rather than each writer deriving it, per the issue's confirmed decision.

**Date:** 2026-10-16
