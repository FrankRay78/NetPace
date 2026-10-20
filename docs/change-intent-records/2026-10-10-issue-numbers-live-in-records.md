# An issue number lives in a change-intent record, and nowhere else in the codebase

**Intent:** Give an issue number one home, so neither an author nor a reviewer has to weigh what a particular number was *for*.

**Behaviour:** `CLAUDE.md` carries the rule as a paired `Don't … → …` line, with its reasoning; `docs/conventions/change-intent-records.md` carries it from the record's side. Issue #344 has the three shapes the numbers were accumulating in.

**Decisions:**

- *No automated check; the rule is enforced by review.* The harness prompts use illustrative numbers — `/raise-pr 247`, `feature/248-pr-closing-keyword` — that a pattern match cannot tell from a citation. A check would fire on every prompt that documents its command correctly, and teaching it the difference needs exactly the judgement the rule was written to remove.
- *The numbers already in the codebase stay for now.* A dozen or so citations in the constitution and `CLAUDE.md` need a record written before the number can go, and about thirty more sit in the harness prompts, hooks, scripts, workflows and the remaining docs. #344 scopes that sweep to a follow-up rather than making it a condition of the rule landing.

**Date:** 2026-10-10
