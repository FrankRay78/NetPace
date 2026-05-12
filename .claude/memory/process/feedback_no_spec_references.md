---
name: codebase must not reference specs/ paths — specs are ephemeral
description: source files, tests, and docs/ must never link to or quote specs/<NNN>-… paths; specs are deleted after the PR is merged so any reference becomes a dead link
type: feedback
---

Never write `specs/<NNN>-…` references into source code, tests, or `docs/*.md`. That includes comments like `// See specs/001-linux-aot-release/contracts/release-pipeline.md` and Markdown links of the form `[contracts](../specs/<NNN>-…/contracts/…)`.

**Why:** The `specs/<NNN>-…` directory is deleted after the corresponding PR is merged — specs are scaffolding for the planning workflow, not durable reference material. Any line that points at a spec path becomes a dead link the moment the spec is deleted, and the explanation that lived in the spec is no longer reachable from the line that needs it.

**How to apply:**
- For comments in code/tests: inline the relevant invariant or contract detail directly. If "See specs/…" was carrying load, the load needs to move into the comment, the docstring, or the relevant `docs/*.md` file (which IS durable).
- For doc cross-references: link to other `docs/*.md` files (`docs/RELEASING.md`, `docs/conventions/*`, `docs/architecture/*`, etc.) or to the source file itself (`src/...`). Never to `specs/`.
- For temporal phrasing like "introduced in feature 001" — drop it. Docs describe what the system does *now*, not its release history; that's what `git log` is for.
- Spec authoring (inside `specs/<NNN>-…/` itself) is exempt — the spec can cross-reference its own siblings; just don't let those links escape into the durable codebase.

This rule is the codebase-side counterpart of [feedback_docs_no_forward_references.md](feedback_docs_no_forward_references.md): both insist that durable surfaces describe only what's true and persistent today.
