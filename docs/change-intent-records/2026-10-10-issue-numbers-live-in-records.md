# An issue number lives in a change-intent record, and nowhere else in the codebase

**Intent:** Give an issue number one home, so neither an author nor a reviewer has to weigh what a particular number was *for*. Issue #344 names the three reasons numbers accumulated — rationale ("the review of #267 raised a warning period the maintainer did not want"), bare label ("every pull request must close an open issue (issue #332)"), and a pointer to pending work ("adding it there is the post-merge step on issue #319").

**Behaviour:** `CLAUDE.md` carries the rule as a paired `Don't … → …` line; `docs/conventions/change-intent-records.md` carries it from the record's side. Constitution §IX's regression reference is the one exception.

**Constraints:** The reasoning does not move out of the rulebook. §VII's own rationale records what happened when a stance lived only in dated records that no rule reads — it was re-argued on every change — so why a rule exists stays written beside the rule as prose, and only the *number* relocates.

**Decisions:**

- *A rule about place, not about meaning.* The rejected alternative was "cite for rationale, not as a label", which reads well and answers nothing: it asks every author and reviewer to judge what a number meant, and two readers routinely disagree. Place has a yes/no answer — the number is in a record or it is not.
- *A pending-work pointer gets no exception, it gets a replacement.* "The post-merge step on issue #319" is the kind that has already put a false statement on `main`: it points at a to-do on a thread that then closes, so nothing open tracks the work and nothing flags the sentence once it is done. The replacement is to describe the current state and track the outstanding step in an open issue.
- *The stale `traceability` claims were already gone.* #344 was drafted against four sentences in `CLAUDE.md`, §VIII, `docs/agentic-workflow-NetPace.md` and `docs/conventions/testing.md` saying the check was not a required check. All four were corrected at constitution 2.2.2 before this branch was cut. Only the retained 2.1.0 Sync Impact Report still asserted it unqualified, and it now carries the supersession note the 2.0.0 entry already carries — which is why the amendment here is 2.2.3 and not the 2.2.1 the issue predicted.
- *No automated check.* The command prompts use illustrative numbers (`/raise-pr 247`, `feature/248-pr-closing-keyword`) that a pattern match cannot tell from a citation, so the rule is enforced by review.
- *The numbers already in the codebase stay for now.* A dozen or so citations in the constitution and `CLAUDE.md` need a record written first before the number can go; #344 scopes that sweep to a follow-up, and whether `.claude/memory/` counts as the codebase is settled there too.

**Date:** 2026-10-10
