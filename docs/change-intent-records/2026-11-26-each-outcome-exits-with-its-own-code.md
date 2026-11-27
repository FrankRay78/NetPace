# Each Outcome Exits With Its Own Code

**Supersedes:** [`2026-11-15-a-missed-target-exits-three`](2026-11-15-a-missed-target-exits-three.md) — `0`, `1` and `3` are no longer the only codes NetPace chooses, and `2` is no longer free. Also [`2026-11-07-no-servers-is-its-own-exception-type`](2026-11-07-no-servers-is-its-own-exception-type.md) — an `ArgumentException` is now read as an operational fault, not as a usage error.

**Intent:** Let a monitoring check tell a mistake in the cron line from an outage without scraping the error stream, by giving a usage error and a `--fail-on` trigger codes of their own.

**Behaviour:** See issue #585 and its confirmed decisions.

**Constraints:**

- All five codes and their precedence are an interface contract joining the frozen surface at 1.0, so they live in one place, `ExitCode`, that the help text and both commands read.
- Doing it before 1.0 is what makes it cheap: splitting codes afterwards would be a breaking change with a deprecation obligation.

**Decisions:**

- Tell a usage error from a fault by its exception type, `UsageException`, and read anything else as a fault. Rejected: classifying by `ArgumentException`, which the `--server-file` reader also raises for a failure that must exit `1`; and inverting `OperationalFault.Matches`, which would read an unanticipated exception as the user's mistake.
- Read the `--server-file` list after `Validate`. Rejected: holding the failure and raising it later, which loses the stack to say the same thing less plainly.
- List the codes on the root help page only. Rejected: repeating them under `servers`, which returns only `0`, `1` and `2`.

**Date:** 2026-11-26
