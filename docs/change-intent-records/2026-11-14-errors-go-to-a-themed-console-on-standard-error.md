# Errors Go To A Themed Console On Standard Error

**Intent:** Give every error one destination that is not the result, so standard output and a `--file` target carry only the format a script or a log is reading.

**Behaviour:** See [issue #545](https://github.com/FrankRay78/NetPace/issues/545) and `USER_GUIDE.md` → *Where errors are written*.

**Constraints:** `ErrorOutput` is where every error site writes; adding one that writes to the result console puts a stray line back in a reader's stream. Its console carries no width limit, so an error is one unbroken line.

**Decisions:**

- An `IAnsiConsole` over the error stream. Rejected: a plain `TextWriter`, as `NoticeOutput` and `DiagnosticOutput` use. Those two carry no style, but an error is drawn in the theme, and the markup hazard they avoid does not arise here: `WriteErrorLine` hands the message to Spectre.Console as text, never as markup.
- Its own console. Rejected: adding an error leg to the composite the run already builds, which `--quiet` and `--file` reach and which is disposed before the last error is written.
- Records flushed before a failed test's error, so the stream reads what was attempted before what went wrong.

**Date:** 2026-11-14
