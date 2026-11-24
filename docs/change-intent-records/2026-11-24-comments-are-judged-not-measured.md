# Comments Are Judged, Not Measured

**Intent:** State what a comment is for and let judgement decide each case, with no numeric measure of length an agent can aim at instead.

**Behaviour:** See issue #597. `CONVENTIONS.md` → *Code quality* → *Comments* restates gaffer's default rule (FrankRay78/gaffer-claude#44) in brief and adds what is NetPace's own.

**Supersedes:** [2026-11-23-comments-have-one-home-and-a-length](2026-11-23-comments-have-one-home-and-a-length.md) — the length limit, and the carve-outs that existed only to exempt things from it.

**Constraints:** No numeric measure of comment length, in lines, sentences or words, comes back in any form. The sweep in #594 met "one or two lines" by splitting long comments with empty comment lines.

**Decisions:**

- One deciding question: would a competent reader miss the comment if it were gone? Rejected: defining what one comment is so that empty lines count. That closes one loophole and leaves the target to aim at.
- `max_line_length` in `.editorconfig` as a hint. Rejected: treating it as a gate. Neither `dotnet format` nor the analyzers enforce it, and it limits how wide a line is, not how much a comment says.

**Date:** 2026-11-24
