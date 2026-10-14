# One Label Per Issue State

**Intent:** Let the maintainer get a hard review question re-framed from the GitHub issue page, without a local session, and give each pipeline label one predictable job.

**Supersedes:** [`2026-09-26-automated-prespec-confirmation`](2026-09-26-automated-prespec-confirmation.md) — the "or which has no review at all" half of its unanswered-gap clause. That case now clears `confirm` and finishes green rather than running the agent and ending red; the unanswered-or-hedging half is unchanged. Also, in part, the *already-reviewed guard is the one deliberate exception* constraint of [`2026-09-07-automated-prespec-review`](2026-09-07-automated-prespec-review.md): that override now applies only to a `review`-labelled run, and step 1's refine path is reached by the `refine` label instead.

**Behaviour:** Specified in issue #375 — `review` posts the first analysis, `refine` re-frames the answers that asked for help, `confirm` folds the answers in; a label applied in a state it does not fit clears itself and changes nothing, except in the two states that were already red and stay red — `ready` with a surviving review comment, and a review whose answers still hedge. Summarised in [`docs/agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md).

**Constraints:**

- The refine logic stays single-sourced in step 6 of [`.claude/commands/reviewissue.md`](../../.claude/commands/reviewissue.md); the workflow may only add environmental deltas.
- The maintainer is the only user of this pipeline, so a wrong label must be cheap to correct, not reported on.

**Decisions:**

- **Refine is a second mode of `reviewissue.yml`, not its own workflow.** It applies the same command file against the same issue, needs the same sender gate, snapshot and concurrency group, and rewrites the comment the other two paths read — a separate file would duplicate all of that to share none of it.
- **No `mode` input on the manual dispatch.** Its only purpose would have been exercising refine once before merge; label-triggered runs use `main`'s copy anyway, so refine is exercised on a real issue afterwards instead. A dispatch is always a review.
- **The prompt clears the `refine` label, and only after its edit lands or it finds nothing to refine** — the mechanism the issue's *refine outcome check* decision asks for, and the same one the review path uses. The alternative, clearing the label from the workflow unconditionally, was written first and is wrong: the action exits 0 whenever the model finishes its turn, so a refused tool call or a rejected PATCH would clear the label and finish green with the comment untouched — the silent skip this change exists to remove. The cost is that a model forgetting the removal on the no-edit path produces a false red, which re-labelling clears.
- **The refine path checks three post-conditions, and none of them is "the comment changed"**: the review comment survived, the issue body is untouched, and the label came off. Each holds on every legitimate outcome, including the no-edit one, so none can false-alarm. The check that *was* ruled out is a check on the edit itself — a refine run that correctly changes nothing, because every answer is concrete, leaves the comment byte-for-byte as a run that should have re-framed a question and did not, so that one would fail the commonest correct outcome. No report file: the cleared label is the completion signal.
- **The whole-comment PATCH is what makes the first two checks worth their lines.** Step 6 loads one rewrite with a long list of byte-for-byte invariants, and a lost sentinel strands the author's answers where `/confirmissue` can no longer find them — which is now a green skip there, not a red run, so this is the only place it is caught. They also bound `Bash(gh api:*)`, which reaches further than the one PATCH the prompt sanctions and cannot be narrowed by the allowlist, since the method may follow the path as easily as precede it.
- **"No answer asks for help" is left to the agent, not the guard.** Deciding it means applying step 6's hedging dictionary to every answer; that run makes no edit and falls through to the same cleared label, so the outcome matches without a second implementation of the dictionary in shell.
- **A wrong-state trigger is silent.** Rejected: a comment naming the label that would have fitted, and a red run. Both were overkill for a single user who can see the label come off and apply another.
- **Refine reads the comments posted after the review.** An answer slot is one quoted line, so an author with more to say writes it underneath and points the answer at it. Only a comment an answer actually points at counts; the rest is ordinary discussion.

**Date:** 2026-10-14
