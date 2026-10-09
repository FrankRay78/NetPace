# The chain reads each stage's verdict by position

**Intent:** Stop the chain mistaking what a stage's report *quotes* for what the stage *decided* — in both directions (#345).

**Supersedes:** the mechanism only, not the conclusions, of [2026-10-08](2026-10-08-last-review-round-only-reads.md) and [2026-10-07](2026-10-07-verify-reviews-its-own-fixes.md), both of which describe the chain as *scanning* for a verdict vocabulary. The vocabulary is unchanged; it is now read off one line instead of searched for.

**Behaviour:** The verdict is the report's last non-blank line, and no other line is read. See [`docs/agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md) → *Reading the verdict* for what is tolerated on that line, and #345 for the report shapes that drove it.

**Constraints:**

- The verdict is written by a model, so decoration arrives whatever the prompt asks for — #242 and #333 both exist because of it.
- The two failure directions cost differently: a false stop costs the rest of one run done by hand; a false success opens a pull request off unverified work.
- The stub matrix must stay runnable with no model.

**Decisions:**

- **Position, over a stricter prose scan.** *Strict exact line* — only an undecorated verdict at the start of a line counts — is cheaper and makes a decorated verdict a safe stop, but it still cannot tell a stage's own plain verdict from a plain one the report quotes. Position cannot be faked by a quotation, so both the false stop and the false success go away together.
- **Position, over a channel outside the prose** (a file, or structured output the stage writes). More robust and it leaves the report free text, but it is a much larger change and would still need a fail-safe for a stage that records nothing. Revisit only if decoration on the last line turns out not to be the whole problem.
- **Cost accepted: every stage prompt now ends on its verdict**, and a stage that writes one more sentence after it stops the run. That is the trade for deleting pattern tolerance rather than extending it — the maintainer's stated preference during #333 for one plain check over layered heuristics. `verify.md` previously asked for the verdict *first* and showed both templates inside code fences, which actively invited the shape that now reads as unreadable; both are rewritten.
- **An unreadable verdict is reported distinctly, and never retried.** Retrying would spend a second stage's model time on a report the chain already could not read, and the two states send a reader to different places: the stage's own work, or the way it wrote its report.
- **A success verdict is anchored at both ends; a failure verdict only at the start.** Position alone is not enough: anchoring the success patterns only at the start left the line itself able to carry a contradiction, and because the failure pattern is `^FAILED` it cannot see a `FAILED` further along that line. So `VERIFIED branch=x, FAILED reason=suite red` read as a pass — the same false success the branch set out to remove, one line lower down. The failure verdict stays unanchored at the end because its reason is free prose by definition.
- **Only up to three leading spaces are stripped.** Four spaces, or a tab, is markdown's other code block. Stripping indentation without limit let a quotation indent itself into last place, which is the single way a quotation can fake position.
- **A stall is told from an outside kill by the clock, not the exit code.** `timeout --kill-after` and an out-of-memory kill both surface as 137, so elapsed time is the only thing that separates them; reporting a kill as a stall sends whoever reads the closing line hunting a hung stage instead of looking at the machine.

**Date:** 2026-10-09
