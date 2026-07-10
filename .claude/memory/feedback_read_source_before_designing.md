---
name: Read source before designing fixes
description: Before proposing a fix that depends on how a subsystem behaves, read the source that implements that behaviour. One line of actual code beats three rounds of fix proposals built on a wrong assumption.
type: feedback
---

Before designing a fix that depends on how a subsystem behaves, **read the source that implements that behaviour** — not the docs, not your prior mental model, not the test names. Reading the actual code upfront routinely avoids multiple rounds of fix proposals that turn out to be solving the wrong problem.

**Why:** ported from IMS's harness-hardening review (a sibling .NET/spec-kit repo) as a preventive practice, not (yet) from a NetPace-specific incident. In IMS, a subsystem's behaviour was repeatedly guessed at ("the system probably does X") across three iterations of a proposed fix, when reading a single line of the actual implementation would have surfaced the real design gap immediately.

**How to apply:** when a proposed fix rests on "the system does X" / "I believe X happens because...", stop and grep or read the exact code that implements X before sketching the fix. Applies especially to `ISpeedTestService` implementations, provider-specific behaviour in `Clients/{ProviderName}/`, and anywhere a fix is being designed against assumed rather than confirmed behaviour.
