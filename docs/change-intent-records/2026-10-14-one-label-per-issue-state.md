# One Label Per Issue State

**Intent:** Let the maintainer get a hard review question re-framed from the GitHub issue page, without a local session, and give each pipeline label one predictable job.

**Supersedes:** [`2026-09-26-automated-prespec-confirmation`](2026-09-26-automated-prespec-confirmation.md) — the "or which has no review at all" half of its unanswered-gap clause. That case now clears `confirm` and finishes green rather than running the agent and ending red; the unanswered-and-hedging half is unchanged. Also the *already-reviewed guard is the one deliberate exception* constraint of [`2026-09-07-automated-prespec-review`](2026-09-07-automated-prespec-review.md): step 1's refine path is no longer overridden away, it is reached by the `refine` label instead.

**Behaviour:** Specified in issue #375 — `review` posts the first analysis, `refine` re-frames the answers that asked for help, `confirm` folds the answers in; a label applied in any other state clears itself and changes nothing. Summarised in [`docs/agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md).

**Constraints:**

- The refine logic stays single-sourced in step 6 of [`.claude/commands/reviewissue.md`](../../.claude/commands/reviewissue.md); the workflow may only add environmental deltas.
- The maintainer is the only user of this pipeline, so a wrong label must be cheap to correct, not reported on.

**Decisions:**

- **Refine is a second mode of `reviewissue.yml`, not its own workflow.** It applies the same command file against the same issue, needs the same sender gate, snapshot and concurrency group, and rewrites the comment the other two paths read — a separate file would duplicate all of that to share none of it.
- **No `mode` input on the manual dispatch.** Its only purpose would have been exercising refine once before merge; label-triggered runs use `main`'s copy anyway, so refine is exercised on a real issue afterwards instead. A dispatch is always a review.
- **No post-condition check and no report file on the refine path**, unlike the review and confirm paths. A refine run that correctly changes nothing — every answer concrete — leaves the comment byte-for-byte as a run that should have re-framed a question and did not. No check could tell the two apart, so one would turn the commonest correct outcome into a false alarm.
- **The workflow clears the `refine` label, the prompt does not** (the review path does the reverse). The label must come off on the no-edit path too, and that is the path an instruction is likeliest to drop.
- **"No answer asks for help" is left to the agent, not the guard.** Deciding it means applying step 6's hedging dictionary to every answer; that run makes no edit and falls through to the same cleared label, so the outcome matches without a second implementation of the dictionary in shell.
- **A wrong-state trigger is silent.** Rejected: a comment naming the label that would have fitted, and a red run. Both were overkill for a single user who can see the label come off and apply another.
- **Refine reads the comments posted after the review.** An answer slot is one quoted line, so an author with more to say writes it underneath and points the answer at it. Only a comment an answer actually points at counts; the rest is ordinary discussion.

**Date:** 2026-10-14
