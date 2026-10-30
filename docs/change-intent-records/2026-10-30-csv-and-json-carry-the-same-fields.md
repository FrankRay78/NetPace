# CSV And JSON Carry The Same Fields Under The Same Names

**Intent:** Stop every new machine-readable field being decided twice, by stating one rule both formats answer to.

**Behaviour:** See issue #464. The rule is stated in `CLAUDE.md` beside the existing rule that CSV and JSON numbers agree, and in `USER_GUIDE.md` for users.

**Constraints:**

- The rule binds the field *names*, not the shape: CSV always writes every built-in column and leaves a cell empty, while JSON leaves the field out.
- `--device-id` and `--custom-field` describe the run, not the measurement, so they stay out of `NetPace.Core` and out of the default and minimal output.

**Decisions:**

- Custom pairs grouped in one JSON `CustomFields` object. Rejected: top-level properties — a custom key would silently displace a built-in field, making the rule promise a name NetPace does not control.
- A repeated key keeps the last value in JSON but writes every CSV column. Rejected: refusing it, or deduplicating both ways — CSV can carry a repeat and JSON cannot, and policing keys is out of scope.
- `--custom-field` checked by the parser, so a malformed pair is refused in every output mode. Rejected: checking only where the pairs are written — the default output would then drop a typo in silence.

**Date:** 2026-10-30
