# Comments Change With Their Code

**Intent:** Keep the comment rule from being applied as a deletion drive: a summary stating what a member is for stays, and existing comments change only alongside their code or where they are wrong.

**Behaviour:** See issue #601. `CONVENTIONS.md` → *Code quality* → *Comments* restores the `<summary>` clause #598 dropped and adds *No sweeps*. The one-sided wording in gaffer's default is fixed on gaffer (FrankRay78/gaffer-claude#48).

**Constraints:** The rule already left untouched comments alone, and #588 overrode it by ordering a whole-tree sweep. The sweep in #600 deleted over 2,000 lines, 187 of them `<summary>` blocks. The ban is stated outright so that an issue cannot override it by implication.

**Decisions:**

- Fix the rule forward. Rejected: reverting #590 and #598 (and gaffer #38 and #45). That brings back essay-length comments and the line-count padding of #594.

**Date:** 2026-11-25
