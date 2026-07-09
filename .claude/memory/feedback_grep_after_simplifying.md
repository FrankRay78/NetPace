---
name: After simplifying, grep for the removed concept's keywords
description: After dropping or simplifying a feature, grep the whole repo for the concept's keywords — the diff won't surface stale comments and docs in files you didn't directly touch.
type: feedback
---

After removing or simplifying a feature, grep for the concept's keywords across the whole repo — especially in files you didn't directly touch (docs, sibling tests, comment headers, project files, config files) — and update or remove every stale match. The diff alone won't surface them.

**Why:** Comments and docs in adjacent files are invisible to a code diff. When a concept disappears from one file, references to it elsewhere — `<remarks>` blocks on related types, README snippets, doc comments on adjacent test classes, struct/class doc-comments — stay behind and rot silently. They survive code review because the reviewer is looking at the diff, not the rest of the repo. The next person to read those stale comments will be misled about how the system actually works.

**How to apply:**
- After any "remove feature X" or "drop concept Y" change, run a final sweep:
  `grep -rE "<keyword1>|<keyword2>|<old-fieldname>" --include='*.cs' --include='*.csproj' --include='*.md' --include='*.yml' --include='*.yaml' .`
- Pay special attention to: `docs/` (especially `docs/architecture/*` and `docs/conventions/*`), README/USER_GUIDE files, sibling-feature `specs/` still in the tree, and XML doc comments / `<remarks>` blocks on types whose code you didn't touch.
- Treat the diff as **necessary but not sufficient** — comments and docs in adjacent files are invisible to it.
- Simplifications driven by deleting speculative work tend to leave more stale comments than feature additions, because the dropped concept may have been described in many places before it was deleted from any one place.
