---
name: Read source before designing fixes
description: Before proposing a fix that depends on how a subsystem behaves, read the source that implements that behaviour. One line of actual code beats three rounds of fix proposals built on a wrong assumption.
type: feedback
---

Before designing a fix that depends on how a subsystem behaves, **read the source that implements that behaviour** — not the docs, not your prior mental model, not the test names, and **not a diagnosis handed to you in an issue, spec, or ticket**. A confident root-cause written by someone else is an assumption to verify against current HEAD, not a fact — especially when the artefact predates recent commits. Reading the actual code upfront routinely avoids multiple rounds of fix proposals that turn out to be solving the wrong problem.

**Why:** confirmed by a NetPace incident. A `/ship` road-test began from issue #220's confident "servers are probed concurrently" root-cause; a branch and test edit were made straight off it — but `GetFastestServerByLatencyAsync` is a sequential `for` loop, and the flake had already been fixed by #221's `SynchronousProgress`. Three clean-context reviewers had to catch that the whole branch premise was invalid. One `Read` of `OoklaSpeedtest.cs` before editing would have surfaced it immediately. See [[feedback_docs_no_forward_references]] for the related rule that docs (and by extension issues) can drift from current code.

**How to apply:** when a proposed fix rests on "the system does X" / "I believe X happens because..." — or on a root-cause quoted from an issue/spec/ticket — stop and grep or read the exact code that implements X before sketching the fix. When the work *starts* from an issue, treat verifying its diagnosis against HEAD as the first step, before creating a branch or editing. Applies especially to `ISpeedTestService` implementations, provider-specific behaviour in `Clients/{ProviderName}/`, and anywhere a fix is being designed against assumed rather than confirmed behaviour.
