# The Server Option Names What It Takes

**Intent:** Free the `--server` name before NetPace can also name a server by Ookla ID, so neither option has to be read by the shape of its value.

**Behaviour:** See issue #452. `--server-url` does what `--server` did; `--server` stays registered and hidden only to fail the parse naming its replacement, the pattern the retired `--verbosity` option in `src/NetPace.Console/Program.cs` already carries.

**Constraints:** A URL carries no sponsor, so a server named this way keeps the `[Unknown]` placeholder.

**Decisions:**

- Rejected: one `--server` taking either a URL or an ID. Its meaning would be guessed from the value's shape, and a speedtest-cli user reasonably expects an ID.
- Rejected: keeping `--server` as a silent alias. Pre-1.0 has no deprecation period, and an alias keeps the ambiguous name alive exactly where `--server-id` is meant to go.
- Rejected: `--server-name` as a display label for a URL-named server. An ID resolves its own sponsor and a server list file carries a name per entry, and the option would read as a lookup by name, which NetPace does not offer.

**Date:** 2026-11-08
