---
name: Scratch and staging files belong in .claude/scratch/
description: Transient drafts, payloads and working notes go in the repo's gitignored .claude/scratch/ — never /tmp, the OS temp dir, ~/.claude/, or a root-level .scratch/.
type: feedback
---

Write every transient file (issue/spec drafts, API payloads, notes) to `.claude/scratch/` (`mkdir -p` first).

**Why:** Frank rejected an issue draft written to `~/.claude/`: drafts must be visible in the IDE next to the project. Only `.claude/scratch/` is gitignored; a root `.scratch/` pollutes `git status`.

**How to apply:** Applies to every command and skill needing scratch space; the speckit issue commands already follow it. Memory entries go in `.claude/memory/`, not user-level memory.
