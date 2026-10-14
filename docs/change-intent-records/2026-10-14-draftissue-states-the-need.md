# `/draftissue` states the need, and an interface is an outcome

**Intent:** Issues drafted from the backlog should reach `/build` with little supervision. `/draftissue` was settling technical shape before review, duplicating `/reviewissue`, and carried examples from another project; Principle IX's mechanism list banned naming the option, output field or exit code a CLI's users depend on.

**Behaviour:** `.claude/commands/draftissue.md` and constitution §IX (2.3.0). Issue #369 carries the motivation and acceptance criteria.

**Constraints:** `/reviewissue` was already made portable and changes only where it named the old template. The shared review-comment format and `/confirmissue` are untouched.

**Decisions:**

- *No draft-approval pause.* `/reviewissue` and `/confirmissue` are the human checkpoint before `/build`; a second stop at draft time costs a round trip per item. Applies to free-text ideas too — one rule for every input.
- *No technical questions and no Technical notes section.* Technical gaps are `/reviewissue`'s and technical calls `/build`'s. Rejected: keeping an optional section, which every run filled because the command always walked the technical categories.
- *Material technical hints survive as non-binding Source notes, as pointers.* The maintainer's call: the command chooses what is material rather than copying the source word for word or dropping hints behind a link.
- *The source is never edited and there is no duplicate check.* The maintainer declined both: marking the backlog row from the command, and searching open issues before drafting.
- *`backlog <number>` resolves through the Project-specific section, and asks when none is listed.* A bare number stays an existing-issue reference, so the two never collide.
- *An interface contract is outcome, judged by who touches it.* Rejected: deleting web-specific items from the §IX list, which would leave a web API's routes unclassified. The who-touches-it test covers both, and the independence test already gave the same answer.
- *Exact human-readable message wording stays mechanism.* Scripts should not parse prose, so wording is a free choice even where it is user-visible.
- *Maintainer approval for the §IX MINOR amendment:* given by raising #369 ("raising this issue is that approval").

**Date:** 2026-10-14
