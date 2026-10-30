# Test Doubles Are A Tested Contract

**Intent:** Make the test doubles `NetPace.Core` publishes a supported surface, so a consumer who builds on one is not broken by a release that changed it in passing.

**Behaviour:** See [#457](https://github.com/FrankRay78/NetPace/issues/457). Every public operation of every double in `NetPace.Core.Clients.Testing` is covered by a test asserting the behaviour its documentation describes.

**Constraints:** The figures the stub and the variable tester report are what every console snapshot shows, so they could not be tidied — the tests pin them as they stand. Each double's XML documentation is now the specification its tests enforce, so sharpening it and writing the tests was one job.

**Decisions:**

- A double reports the same shape of server as the real provider. Rejected: leaving the stub's `(Unknown)` location, which costs nothing but shows consumers a value the library never produces.
- Pin each double's exact figures. Rejected: asserting only that figures vary, which would pass against figures the console snapshots built on them no longer match.
- `VariableSpeedTester` measures a URL-named server. Rejected: documenting its `NotImplementedException` as intended, leaving a published double that fails one of its interface's routes.

**Date:** 2026-10-30
