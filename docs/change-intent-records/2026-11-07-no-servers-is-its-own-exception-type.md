# "No Servers" Is Its Own Exception Type

**Supersedes:** [2026-10-06-screening-separate-from-measuring](2026-10-06-screening-separate-from-measuring.md) — the decision that the "nothing answered" case keeps throwing a bare `Exception`.

**Intent:** Let "no server could be found or reached" be handled on purpose — by NetPace's own commands and by a `NetPace.Core` consumer — instead of only by a catch-all.

**Behaviour:** See issue #495.

**Constraints:** The message is load-bearing, printed verbatim by the console, so the new type carries it as its default and no throw site keeps a literal of its own.

**Decisions:**

- The earlier record kept a bare `Exception` because the message said enough. Reversed: a message is not something code can branch on, so a handler wanting this one condition had to catch everything — including defects it then reported as a network outcome.
- One type for all five throw sites, rather than one per site. Rejected: a per-site type tells a consumer where the condition arose, which is NetPace's business and not theirs.
- `ArgumentException` still exits non-zero from `servers`, outside the network-outcome split: a provider its settings have made unusable is a usage error, not a verdict on the network.

**Date:** 2026-11-07
