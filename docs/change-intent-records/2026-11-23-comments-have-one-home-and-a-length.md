# Comments Have One Home And A Length

**Intent:** Bound what a code comment is for and how long it may be, so an unattended build writes the non-obvious why in a line or two and sends longer reasoning where the decision table points.

**Behaviour:** See the issue. `CONVENTIONS.md` → *Code quality* → *Comments* is the rule; `REVIEW.md` grades a breach.

**Constraints:** `SCENARIO:` markers, Given / When / Then labels and harness file headers are required elsewhere in the same conventions, two of them gated by CI, so the rule names them structure rather than narration.

**Decisions:**

- Restated here, not left to gaffer's default. Rejected: pointing at the default, which the default prefers — but `CLAUDE.md` loads this file as session context, so a restated rule reaches every session, not only a running command.
- A breach is Minor. Rejected: the Major its *Code quality* heading would give it, ranking a long comment with a swallowed exception.
- One home, the guides pointing at it. Rejected: each guide restating the rule — three copies of a length drift apart, as one already had.

**Date:** 2026-11-23
