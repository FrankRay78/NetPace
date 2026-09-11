# Confirmed Decisions Replace the Review Comment

**Intent:** Hand whoever picks up a confirmed issue the decisions, not the deliberation that produced them. `/speckit.confirmissue` deletes the review comment once it has folded the answers into `## Confirmed decisions`, and the "this issue has been through the pre-spec gate" signal moves off the comment's existence and onto the `ready` label.

**Supersedes:** decision 4 of [`2026-09-07-automated-prespec-review`](2026-09-07-automated-prespec-review.md), which made "unlabelled with a `<!-- speckit:review -->` comment" the done-marker, and the behaviour clause in that record that assumes the comment survives confirmation. The rest of that record — the trigger gating, the snapshot, the post-condition check, the concurrency reasoning — stands unchanged.

**Behaviour:**
- Given: an issue whose review has every gap answered
- When: `/speckit.confirmissue` runs and the decisions reach the issue body
- Then: the issue carries `## Confirmed decisions` and the `ready` label, and the review comment is gone from the thread.
- Given: a run that stops before the decisions reach the body — an unanswered gap, a hedging answer, a failed body patch
- Then: the review is still on the issue, answerable and re-runnable exactly as before.
- Given: a run whose decisions are saved but whose `ready` label does not apply, or whose deletion call fails
- Then: the run does not fail, the decisions are not lost, the review is still on the issue, and the report says so.
- Given: an issue that has already been confirmed
- When: a review is requested for it — locally via `/speckit.reviewissue`, or by applying the `review` label
- Then: no review is posted, and the requester is told the issue is already confirmed.
- Given: a confirmed issue whose scope has since moved
- When: the author removes the `ready` label and requests a review again
- Then: a fresh first review is posted, with no hand-editing of the issue body.
- Given: an issue confirmed before this change, whose review comment still exists
- Then: the sentinel still marks it as already reviewed, so it is not re-reviewed.
- Given: a review posted but not yet confirmed
- When: `/speckit.reviewissue` is re-run on it
- Then: the refine path runs exactly as before.

**Constraints:**
- Deletion is irreversible and silent — GitHub sends no notification and offers no undo. Ordering is therefore load-bearing: the body patch must be confirmed landed, and the `ready` label applied, before the comment is removed. Every hard-stop in `/speckit.confirmissue` sits upstream of the deletion.
- The done-marker cannot be removed before its replacement is in place. `ready` is applied by a step that is explicitly *never fatal*, so deletion is made conditional on that step having succeeded; a run whose label did not land keeps the review.
- Two consumers key on "has this issue been reviewed": [`speckit.reviewissue.md`](../../.claude/commands/speckit.reviewissue.md) step 1, and the guard step in [`speckit-reviewissue.yml`](../../.github/workflows/speckit-reviewissue.yml). Both had to learn the new marker in the same change, or a confirmed issue would be re-reviewed on top of its own decisions.
- Issues confirmed under the old behaviour keep their review comments. The sentinel condition stays, so they continue to read as already reviewed with no migration.

**Decisions:**
1. **The `ready` label is the done-marker, not the `## Confirmed decisions` section** — both survive confirmation, and the technical sketch on the issue proposed either. A section in the body cannot be the marker, because the only way to clear it is to hand-edit the body, and putting a confirmed issue back into review must not require that. Removing `ready` is the natural signal that scope has moved: an issue whose decisions no longer hold is by definition no longer ready. Rejected: keying on the body section (no clean reopen); keying on both (same problem, since either half alone blocks).
2. **Deletion is conditional on the label having landed** — the alternative, deleting unconditionally after a successful body patch, has a real failure mode: `ready` is never-fatal by design, so a label failure followed by a deletion leaves an issue with decisions on its body, no label, and no comment — indistinguishable from never-reviewed to both consumers, and it would be re-reviewed. Keeping the comment when the label fails means the old marker covers the gap until the new one is in place. Rejected: delete-then-label (inverts the dependency); delete regardless (manufactures the exact state the guard exists to prevent).
3. **`/speckit.confirmissue` re-applies `ready` when it finds an already-confirmed issue** — that is the repair path for decision 2's failure case, and it costs one additive, never-fatal call on a path that was previously a bare stop. It also fixes a misreport: an already-confirmed issue used to be told "no review comment to confirm", which reads as "never reviewed" — the opposite of the truth.
4. **Bare deletion, no tombstone comment** — a one-line *"review folded into the issue body — N decisions"* comment was considered and dropped. Once `ready` and `## Confirmed decisions` carry the state, a tombstone adds a comment to restate what the body already says, which is the noise this change exists to remove. Worth revisiting only if the missing thread causality is ever actually missed.
5. **Hand-editing `## Confirmed decisions` becomes the supported way to revise a decision** — the command previously declared the section command-owned and human edits clobbered on re-run, on the reasoning that the section could always be regenerated from the review. With the review deleted there is no source to regenerate from, so the claim is ownership the command can no longer exercise. The replace path now only fires when a *new* review has been posted and answered, which takes the deliberate reopen. Rejected: keeping the ownership claim (false); merging new decisions into an existing section (changes what gets folded, which this change deliberately left alone).
6. **The guard reads `ready` from the snapshot, the sentinel from a paginated comment fetch** — labels arrive whole in `.claude/scratch/issue-context.json`, which the run already took; comments do not, and a sentinel below the page boundary would be invisible. Checking the label first also makes the cheap condition the common one: a confirmed issue skips without a paginated comment read.

**Known residuals:**
- **An issue labelled `ready` by hand, without ever being reviewed, is treated as confirmed.** `/speckit.reviewissue` stops and points at a `## Confirmed decisions` section that does not exist. Recoverable in one step — remove the label — and the stop message says how, but the diagnosis is briefly confusing. Accepted over a second condition that would have to distinguish "ready because confirmed" from "ready because someone said so".
- **Deleting the comment loses the gap framing, the cited evidence and the recommendations.** That is the point of the change, but it is genuinely irreversible: the confirmed-decision bullets carry each gap's title, its outcome, and the redirect note where the author overruled the recommendation, and nothing else survives. A decision whose *reason* was only ever in the recommendation text is not recoverable afterwards.
- **`## Confirmed decisions` still may not reach the implementer.** [`build.md`](../../.claude/commands/build.md) treats the `## Acceptance criteria` checklist as the criteria and never mentions confirmed decisions. Removing the comment does not cause that, but it does remove the only other place the information existed. Tracked separately.

**Date:** 2026-09-11
