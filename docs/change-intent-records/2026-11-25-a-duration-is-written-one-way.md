# A Duration Is Written One Way

**Intent:** Give `--time-limit` and `--delay` one set of accepted forms, so neither can read a typed duration as something the user did not mean.

**Behaviour:** See issue #581 and its confirmed decisions. `DurationOption` is the one parser both options use.

**Constraints:** The accepted set is a CLI contract, so a form added later is a form NetPace keeps. `--delay` was `Option<TimeSpan>`; the narrowing breaks it, which the pre-1.0 stance allows (`CONVENTIONS.md` → *Public API and breaking changes*).

**Decisions:**

- A bare number is refused. Rejected: reading it as seconds, which `timeout` and `sleep` do. It is the one reading that cannot be checked by being wrong loudly, and `--delay` had been binding it as days.
- Lower-case unit letters only. Rejected: case-insensitive matching, because `M` means minutes in some tools and months in others, so accepting it is a guess.
- A thirty-day ceiling on the suffixed form. Rejected: no ceiling, which would let a typo bound a run past anything watching it. The colon form keeps its own 23:59:59, which `hh` imposes; both ceilings are named per form wherever either is stated.
- Compound (`1h30m`) and fractional (`1.5h`) forms left out. Both can be added later without breaking what is accepted now.

**Date:** 2026-11-25
