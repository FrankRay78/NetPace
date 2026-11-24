# A Server File Lists The Candidates

**Intent:** Let a run choose from a fixed set of servers, so results logged over weeks compare, and let a self-hosted server carry a name rather than `[Unknown]`.

**Behaviour:** See issue #596 and its confirmed decisions. `--server-file` takes a local JSON array of `{"url": …}` entries, each with an optional `"name"`; selection then runs unchanged over them.

**Supersedes:** [2026-11-08-server-option-names-what-it-takes](2026-11-08-server-option-names-what-it-takes.md) — `--server-id` is dropped, so no Ookla ID option is coming and an entry carries no ID.

**Constraints:** No URL shape separates an Ookla server from a LibreSpeed one, so an entry's values cannot be checked before the run.

**Decisions:**

- Rejected: Ookla's field names, `sponsor` for the shown name and `name` for the location. Two providers share this file, and only `url` is common to both.
- Rejected: checking `url` up front. A wrong-kind or unreachable entry can only lose in selection, where an unreachable discovered server already does.
- Rejected: a reader in `NetPace.Core`. Screening a caller-supplied list is already public API, so the console project reads the file alone.
- Rejected: a provider field in the file, and `--server-file` on `netpace servers`. `--provider` already says what the entries are, and nobody but the user writes one.

**Date:** 2026-11-24
