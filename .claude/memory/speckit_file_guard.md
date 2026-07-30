---
name: speckit file guard is Edit-only by design
description: The spec-kit deny rules use one Edit(path) rule per path — an Edit rule covers Write too, so parallel Write/MultiEdit rules only produce startup warnings.
type: reference
---

`.claude/settings.json` `permissions.deny` blocks `Edit(<path>)` on the upstream-managed spec-kit files that carry local customizations but are **not** extension points: `.claude/skills/speckit-*/SKILL.md`, `.specify/templates/*.md`, `.specify/scripts/bash/*.sh`. `deny` overrides the blanket `Edit(**)` allow and `defaultMode: acceptEdits`, so an agent is stopped at edit-time (rationale: [`docs/change-intent-records/2026-07-10-guard-speckit-files.md`](../../docs/change-intent-records/2026-07-10-guard-speckit-files.md)).

**One `Edit(path)` rule per path is the whole guard.** File-permission checks consult `Edit(path)` rules only, and an `Edit` rule covers every file-editing tool — `Write` included. A parallel `Write(path)` deny line is accepted but never consulted; `MultiEdit` is a legacy tool name matching no tool at all. Both deny nothing and emit a startup warning apiece. The same applies in `allow`: `Read(**)` covers `Glob`, `Edit(**)` covers `Write`.

**Why:** the guard shipped with `Edit`/`Write`/`MultiEdit` triples on the belief that `Edit` alone left a hole a `Write` could slip through. It didn't — those rules plus the equally inert `Glob(**)`/`Write(**)`/`MultiEdit(**)` allow entries produced twelve startup warnings and zero extra protection. Removed 2026-07-30 (#233).

**How to apply:**
- Adding a protected path: write **one** `Edit(<path>)` deny rule. Don't "strengthen" it with `Write`/`MultiEdit` twins.
- Permission rules only — hook `matcher` fields are a separate mechanism that matches tool names directly and is unaffected by this.
- A regression is visible at launch: the startup warning names the offending rule.
