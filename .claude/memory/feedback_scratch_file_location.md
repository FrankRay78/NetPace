---
name: Scratch and staging files belong in .claude/scratch/
description: Transient drafts, payloads and working notes go in the repo's gitignored .claude/scratch/ — never /tmp, the OS temp dir, ~/.claude/, or a root-level .scratch/. Headless CI agents use ci-scratch/ instead.
type: feedback
---

Write every transient file (issue/spec drafts, API payloads, notes) to `.claude/scratch/` (`mkdir -p` first).

**Why:** Frank rejected an issue draft written to `~/.claude/`: drafts must be visible in the IDE next to the project. Only `.claude/scratch/` is gitignored; a root `.scratch/` pollutes `git status`.

**How to apply:** Applies to every command and skill run interactively; the issue draft/review/confirm commands already follow it. A headless CI agent (claude-code-action) is refused writes under `.claude/`, so CI prompts redirect the agent's scratch files to `ci-scratch/` (see `confirmissue.yml`); plain runner shell steps can still use `.claude/scratch/`. Memory entries go in `.claude/memory/`, not user-level memory.
