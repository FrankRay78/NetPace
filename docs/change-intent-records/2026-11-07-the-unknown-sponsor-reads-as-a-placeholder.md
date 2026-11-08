# The Unknown Sponsor Reads As A Placeholder

**Intent:** Make the sponsor NetPace invents for a server named by URL alone read as a placeholder rather than as an aside.

**Behaviour:** See issue #451. `[Unknown]` replaces `(Unknown)` at every site that mints it, so default output, CSV, JSON and `NetPace.Core` consumers all read the same value.

**Constraints:**

- Square brackets are Spectre.Console's markup delimiters, and `AnsiConsole.MarkupLine` on this value throws rather than printing it. Every sink writes the sponsor as plain text today; a new one must keep that or escape it.
- The value is an interface contract — the `ServerSponsor` field of CSV and JSON output, and the `Sponsor` of a published return type — so a script or library consumer comparing against `(Unknown)` breaks.
- Every site takes the value from the one constant `Server.UnknownSponsor`, so the URL path with and without a latency test cannot drift apart. Tests assert the literal `[Unknown]`, not the constant, because the literal is the contract.

**Decisions:**

- Keeping `(Unknown)`. Rejected: parentheses read as an aside beside a real name, where brackets read as a stand-in for a name that is missing.
- Leaving the sponsor empty for such a server, as its location already is. Rejected: a reader of the default output is then told nothing at all about the server they named.

**Date:** 2026-11-07
