# A Triageable Pre-Specification Review

**Intent:** Let the author of an issue spend attention on a `/speckit.reviewissue` comment in proportion to what is at stake, and stop the command's fourteen-category sweep inflating the review beyond what the issue actually leaves open — without dropping, demoting or capping a single gap on merit.

**Behaviour:**

- Given a review raising several gaps, When the author opens the comment, Then an at-a-glance table gives every gap's number, title and consequence before any gap body is read.
- Given gaps that differ in consequence, When the comment is composed, Then within each group the gaps that change what the issue commits to building are numbered ahead of those that settle a detail, and each gap names its own kind.
- Given an issue that leaves little open, When the review runs, Then it raises gaps only where something is genuinely unsettled, and is visibly shorter than the review of an issue that leaves much open.
- Given a gap of any consequence, When it is composed, Then it still carries a recommendation with a one-sentence reason and an inline answer slot, in the form `/speckit.confirmissue` already parses.

**Constraints:**

- `/speckit.confirmissue`'s prompt is out of scope, so its parser is fixed: it folds every `**N. <title>**` block ending in a `> _Answer:_` line into the issue body. Anything added above the gaps must match neither shape.
- The Requirements-then-Technical group order is load-bearing for that command's routing — it files each folded decision by the group its gap sat under. Consequence ordering therefore operates strictly *within* a group.
- The author refers to gaps by number in chat, and folding strips the numbers, so ordering must be settled before numbering and a posted review is never renumbered.
- Prompt-only change. Per Constitution Principle I's configuration-and-tooling carve-out there is no xUnit test and no bespoke checker; the evidence is the command's own output before and after, recorded in the PR body.

**Decisions:**

1. **Consequence is two values, not a severity scale.** *Changes what gets built* versus *Settles a detail*, decided by a question about the artefact — would a different answer change something the issue commits to building? Rejected: a three-tier priority, which offers a middle bucket everything drifts into and still does not say *what* would change. Borderline calls take the lower kind, because an inflated marker costs the reader precisely the attention the marker exists to save.

2. **The up-front view is a table, and a downstream parser chose its shape.** The obvious index mirrors the gaps — a bold-numbered list — and that is exactly the shape `/speckit.confirmissue` folds, so every row would have landed in the issue body as a phantom decision the author never made. A table row carries a bare number and plain text and matches nothing the parser looks for. Also rejected: a collapsed `<details>` block, which leaves the reader an action to perform before they can see the set, which is the behaviour being removed.

3. **The table is always emitted, even for a single gap.** Rejected: a threshold below which it is omitted. A threshold makes the reader establish whether the table is missing or merely absent before trusting it, and gives the refine run a mode in which a row may or may not exist.

4. **The sweep is narrowed at source by an open-ness test, not by a cap.** A category considered and found settled produces nothing. Rejected: a numeric cap on gaps, which forces out a genuine gap for arriving last; and the superseded #278 proposal of demoting the gaps the reviewer predicts the author would not contest, which asks the model to judge a person rather than the issue and settles a real decision silently when it judges wrong.

5. **The length bound is per-gap words, and splitting is explicitly not the remedy.** The rule it replaces bounded sub-bullets and offered "consider whether it is actually two gaps" as the fix — which raises the gap count while leaving the word count where it was. 120 words was chosen against real output: the four gaps of the review on #278 ran 184, 192, 196 and 317 words, so the bound bites on every one of them while still leaving room for framing, cited evidence, two or three sub-questions and a reasoned recommendation.

6. **A refine run does not retrofit the new shape.** A comment posted before the table and the consequence line existed keeps neither. Re-framing a hedging gap updates that gap's row if it has one, and nothing else.

**Residual risks:**

- **The consequence call is an unverified model judgement.** #281's own open questions name the fallback if it proves badly calibrated: order by consequence without publishing the marker, which keeps the benefit that survives a wrong call and drops the one that does not.
- **The 120-word bound and the open-ness test are enforced by instruction, not by a gate** — against this repo's precedent of preferring a gate (CIR [`2026-09-07-automated-prespec-review`](2026-09-07-automated-prespec-review.md), decision 5). `/speckit.reviewissue` composes prose, and there is no exit code that decides whether a gap was manufactured.
- **`/speckit.draftissue` is a third consumer of this comment.** It ingests the whole body as brief material in migration mode and reads it as prose rather than parsing its shape, so the table breaks nothing — but each gap title now reaches it twice. Enumerated here deliberately: the same consumer was missed by reviews of this section on the two preceding issues (`docs/study/279.md`, `docs/study/280.md`).
- **Nothing verifies that a recommendation is a good default, or that its *Reason* survives being checked against the file it cites.** A shorter review makes each recommendation carry more weight, so this change raises the cost of that gap without addressing it. #281 records it as separate and more consequential than length.

**Date:** 2026-09-12
