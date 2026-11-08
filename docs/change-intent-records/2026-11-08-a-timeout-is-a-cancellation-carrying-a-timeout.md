# A Timeout Is A Cancellation Carrying A Timeout

**Intent:** Give `NetPace.Core` consumers a timeout they can recognise, so a timed-out request stops being indistinguishable from the caller asking to stop.

**Behaviour:** See [issue #524](https://github.com/FrankRay78/NetPace/issues/524) and its confirmed decisions.

**Constraints:** A timeout and a cancellation are the same exception type, so only what the exception carries can tell them apart. The console decides separately, from its own token: a cancellation is the user's only when the command's token is cancelled.

**Decisions:**

- The latency probe's per-request timeout is raised as .NET's HTTP client raises its own `Timeout` — a cancellation with a `TimeoutException` inside — so the server-list fetch, which already times out that way, needs nothing. Rejected: a NetPace-specific exception type. It leaves consumers two shapes to learn where one already exists.
- Rejected: letting the console decide by its token alone, with no library change. The console would then be guessing what a cancellation it did not ask for means, and a library consumer would still have nothing to read.

**Date:** 2026-11-08
