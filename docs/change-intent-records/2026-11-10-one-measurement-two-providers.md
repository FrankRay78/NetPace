# One Measurement, Two Providers

**Intent:** Add a LibreSpeed provider without a second copy of the measurement code, and without opening NetPace.Core's provider shape as public API before a third provider proves it.

**Behaviour:** See [#537](https://github.com/FrankRay78/NetPace/issues/537), whose technical spike settled the shape and records the reasoning in full.

**Constraints:** `NetPace.Core` ships to NuGet, so every override point widened here is API forever. The base class and the base settings record are therefore public but closed to outside inheritance: an `internal` constructor and `private protected` override points. Widening them later is non-breaking, so it waits. A provider-agnostic type must not reach forward into a provider, so the shared settings hold the profile ceilings and connection counts while each provider's record holds its own request sizes.

**Decisions:**

- A base class implementing `ISpeedTestService` once. Rejected: a provider of its own copying ~1,000 lines of concurrency code, most of it guarding a fixed bug, so every later fix is made twice and LibreSpeed ships either duplicating ~180 tests or less tested than Ookla.
- Rejected: an internal engine each provider holds. It shares the measurement equally well, but every provider then repeats all twelve interface overloads, their guards and their XML docs to forward each call.
- Rejected: opening the class to outside inheritance now, on two providers. A download request already differs between them, an image size against a MiB count, so the override shape is not yet settled enough to publish.

**Date:** 2026-11-10
