# The chain reads each stage's verdict by position

**Intent:** Stop the chain mistaking what a stage's report *quotes* for what the stage *decided* — in both directions (#345).

**Behaviour:** [`docs/agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md) → *Reading the verdict*, which also says what decoration is tolerated on that line.

**Constraints:** The two failure directions cost differently — a false stop costs the rest of one run done by hand, a false success opens a pull request off unverified work.

**Decisions:** Both alternatives the delta doc names as weighed and rejected:

- *Rejected: a channel outside the prose* — a file, or structured output each stage writes. More robust, and it would leave the report free text, but it is a much larger change and would still need a fail-safe for a stage that records nothing. Revisit only if decoration on the last line turns out not to be the whole problem.
- *Rejected: a stricter prose scan.* Counting only an undecorated verdict at the start of a line is cheaper, and makes a decorated verdict a safe stop, but it still cannot tell a stage's own plain verdict from a plain one the report quotes. Position cannot be faked by a quotation, so both failure directions go away together.

**Date:** 2026-10-09
