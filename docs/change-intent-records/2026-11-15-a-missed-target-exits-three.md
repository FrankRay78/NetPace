# A Missed Target Exits Three, And Its Units Are Read In The Console

**Intent:** Let a scheduled run say whether the line met its targets, so cron or a monitoring check can act on the exit code with no wrapper script.

**Behaviour:** See issue #556 and its confirmed decisions.

**Constraints:**

- `0`, `1` and `3` are now the only codes NetPace itself chooses, and `3` is an interface contract a monitoring check reads: it joins the frozen surface at 1.0.
- The suffixes a target accepts are `NetPace.Console`'s own list, so `NetPace.Core` gains no parser and no public member. A test reads every suffix the library writes back through the parser, so a unit the library gains or renames shows up as a threshold value the CLI refuses.

**Decisions:**

- Exit `3`, leaving `2` free for the usage-error convention many tools follow.
- Parse the unit in the Console. Rejected: a text parser on `NetPace.Core`'s public surface, which would bind the library to a CLI concern that a consumer comparing result records does not need.
- Refuse a target under `--loop` or `--count` above 1. Rejected: answering for the last run, since one exit code cannot say which of fifty tests fell short.
- Miss lines to the error stream. Rejected: the result stream, where a sentence of English would land inside CSV or JSON another program is reading.

**Date:** 2026-11-15
