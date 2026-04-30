---
name: Scratch and staging files belong in .claude/scratch/
description: Use .claude/scratch/ in the current repo as the required scratch location for transient drafts, staging files, and working notes — never /tmp, the system temp dir, or ~/.claude/.
type: feedback
---

The required scratch location for any transient or staging file (issue drafts, spec drafts, working notes, intermediate API payloads) is **`.claude/scratch/`** in the current repo. Do **not** use `/tmp`, the system temp dir (`%TEMP%`, `$TMPDIR`), or the user-level `~/.claude/` (`C:\Users\frank\.claude\`) directory.

The directory is git-ignored (`.claude/scratch/` is listed in `.gitignore`), so files written there will not show up in `git status`. Run `mkdir -p .claude/scratch` before writing if you're not certain the directory exists yet.

**Why:** During issue drafting, Claude wrote the issue body to `C:\Users\frank\.claude\gh-issue-body-cli-profile-switch.md`. User rejected: "please write somewhere local in this repo, as this is a temp staging file." Working drafts must be visible in the IDE alongside the project — not buried in tool config, not buried in `/tmp` (which is invisible to Windows-side tooling and broken on native Windows shells), and not buried in the system temp dir. The same principle applies to repo memory —  `CLAUDE.md` deprecates user-level memory and points to repo-tracked `.claude/memory/`.

**How to apply:** Any tool, skill, or command that needs scratch space writes to `.claude/scratch/<purpose>.md` (or `.json`, etc.). This is canonical, not a fallback — the cross-OS cascade ("try /tmp, then OS temp, then ~/.claude/") that some commands historically used is wrong for this user. The three speckit commands (`/speckit.draftissue`, `/speckit.reviewissue`, `/speckit.confirmissue`) all standardize on this. For memory entries specifically: write to `.claude/memory/` in the repo, not the user-level memory directory.
