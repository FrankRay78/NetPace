---
name: After adding or removing a rule, sweep for what it touches
description: After removing a concept, grep the whole repo for its keywords; after adding a rule to a command prompt, search that prompt for older rules it overrides and every path it applies to.
type: feedback
---

After removing or simplifying a feature, grep for the concept's keywords across the whole repo — especially in files you didn't directly touch (docs, sibling tests, comment headers, project files, config files) — and update or remove every stale match. The diff alone won't surface them.

After **adding** a rule to a command prompt, run the mirror-image sweep inside that prompt: find every older rule the new one overrides or qualifies, and reword those in the same change; and find every path the rule applies to (first run, refine/re-run, automated workflow) and scope it for each.

**Why:** Comments and docs in adjacent files are invisible to a code diff. When a concept disappears from one file, references to it elsewhere — `<remarks>` blocks on related types, README snippets, doc comments on adjacent test classes, struct/class doc-comments — stay behind and rot silently. They survive code review because the reviewer is looking at the diff, not the rest of the repo. The adding case is the same blind spot: in `speckit.reviewissue.md`, #286's new self-check clause was not scoped against step 6's refine-run rules, and #287's new Reason rule then contradicted the older quality bar and step 6 and was again not scoped to the refine run — both caught only by `/verify` reviewers (`docs/study/286.md`, `docs/study/287.md`).

**How to apply:**
- After any "remove feature X" or "drop concept Y" change, run a final sweep:
  `grep -rE "<keyword1>|<keyword2>|<old-fieldname>" --include='*.cs' --include='*.csproj' --include='*.md' --include='*.yml' --include='*.yaml' .`
- Pay special attention to: `docs/` (especially `docs/architecture/*` and `docs/conventions/*`), README/USER_GUIDE files, sibling-feature `specs/` still in the tree, and XML doc comments / `<remarks>` blocks on types whose code you didn't touch.
- Treat the diff as **necessary but not sufficient** — comments and docs in adjacent files are invisible to it.
- Simplifications driven by deleting speculative work tend to leave more stale comments than feature additions, because the dropped concept may have been described in many places before it was deleted from any one place.
- After adding a rule to a prompt, grep that prompt for the rule's subject (e.g. `Reason`, `refine run`, `step 6`) before committing, and reconcile every hit.
