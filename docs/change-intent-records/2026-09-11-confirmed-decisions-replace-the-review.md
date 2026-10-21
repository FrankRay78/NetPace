# Confirmed Decisions Replace the Review Comment

**Intent:** Hand whoever picks up a confirmed issue the decisions, not the deliberation that produced them. `/confirmissue` deletes the review comment once it has folded the answers into `## Confirmed decisions`, and the "this issue has been through the gate" signal moves off the comment's existence and onto the `ready` label.

**Behaviour:** [`.claude/commands/confirmissue.md`](../../.claude/commands/confirmissue.md), whose step 7 carries the ordering and the read-back that gate the deletion; `docs/agentic-workflow-NetPace.md` → *The whole pre-build issue gate runs in CI*.

**Constraints:** Deletion is irreversible and silent — GitHub sends no notification and offers no undo — so the body patch must be confirmed landed, and `ready` applied, before the comment goes.

**Decisions:**

- *Rejected — a tombstone comment.* A one-line *"review folded into the issue body — N decisions"* was considered and dropped. Once `ready` and `## Confirmed decisions` carry the state, a tombstone adds a comment to restate what the body already says, which is the noise this change exists to remove. Worth revisiting only if the missing thread causality is ever actually missed.

**Known residual:**

- **"No `ready`, no comment, decisions on the body" is two different situations that nothing can tell apart.** It is the deliberate reopen signal, and it is also what a lost label looks like — a repository-level label rename or delete strips `ready` from every issue at once, silently. Both consumers read it as "never reviewed" and post a fresh review; on the reopen path that is the intent, and on the lost-label path it is the failure this design exists to prevent. Distinguishing them needs an explicit reopen signal rather than an inferred one.

**Date:** 2026-09-11
