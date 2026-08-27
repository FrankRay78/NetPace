# Guarding Upstream-Managed Spec-Kit Files from Agent Edits

**Intent:** Stop a headless agent (or a stray `specify init --force` regen) from silently mutating the spec-kit files that carry local customizations but are *not* extension points — the failure mode that flipped `disable-model-invocation: true → false` across the SDD skills during the 0.12.10 upgrade.

**Behaviour:**
- Given: an agent attempts to edit a protected file (`.claude/skills/speckit-*/SKILL.md`, `.specify/templates/*.md`, or `.specify/scripts/bash/*.sh`) with any file-editing tool
- When: the tool call is evaluated against `.claude/settings.json`
- Then: it is denied, overriding the blanket `Edit(**)` allow and `defaultMode: acceptEdits`.
- Given: an agent edits a genuine extension point (`.specify/templates/overrides/**`, `.specify/extensions/**`, `.specify/memory/constitution.md`, `.specify/feature.json`)
- Then: the edit is allowed — the deny globs do not match these.

**Constraints:**
- The protected set mirrors the two spec-kit manifests (`.specify/integrations/{speckit,claude}.manifest.json`): 6 core templates, 5 bash scripts, 10 SDD skills.
- Extension points must stay editable — deny globs are single-level (`*` never crosses `/`), so `templates/*.md` excludes `templates/overrides/`.
- Enforcement must cherry-pick cleanly onto other repos sharing this harness: config-only, no per-clone bootstrap.

**Decisions:**
1. **`deny`, not `ask`** — a firmer guarantee against headless drift. Trade-off: a deliberate human edit requires temporarily removing the rule. `ask` (human-approvable, headless-blocked) was rejected as too soft for the stated goal.
2. **`.claude/settings.json` permissions, not a git pre-commit hook or CI check** — the threat is agent edits, so gate at edit-time via the harness. Rejected: git hook (fires late, needs `core.hooksPath` bootstrap, `--no-verify`-able); CI invariant check (catches upgrade-clobber, a manual one-off, not the agent-drift case).
3. **`speckit-*` glob over enumerating the manifest's exact 10 skills** — a glob survives new SDD skills (e.g. `speckit-converge`) and can't fail open the way a clever character-class exclusion could. Side effect: also protects the 5 `speckit-git-*` extension skills — a harmless, arguably desirable superset.
4. **One `Edit(path)` rule per protected path, not parallel `Write`/`MultiEdit` rules** *(amended 2026-07-30)* — file-permission checks consult `Edit(path)` rules only, and an `Edit` rule already covers every file-editing tool. This CIR originally shipped parallel `Write(path)`/`MultiEdit(path)` deny lines on the belief that `Edit` alone left a hole; they denied nothing and emitted a startup warning apiece, so they were removed. The guard is unchanged in strength. Do not re-add them.

**Known residual:** the guard covers the file-editing tool path only; a `sed -i`/`git checkout` via `Bash` can still overwrite these files. Out of scope here — a manifest-vs-deny-glob CI check would be the backstop if durable protection is ever needed.

**Date:** 2026-07-10
