---
name: Scratch and staging files belong in the repo
description: Write temp drafts, scratch artefacts, and staging files into the current repo, not into the user's global ~/.claude/ directory.
type: feedback
---

When generating temp or staging files (issue drafts, spec drafts, scratch artefacts, working notes), write them into the current repo (e.g. `<repo-root>/<scratch-name>.md` or a designated repo scratch directory), not into the user's global `~/.claude/` (`C:\Users\frank\.claude\`) directory.

**Why:** During issue #174 drafting, Claude wrote the issue body to `C:\Users\frank\.claude\gh-issue-body-cli-profile-switch.md`. User rejected: "please write somewhere local in this repo, as this is a temp staging file." User wants working drafts visible in the IDE alongside the project, not buried in their tool config directory where they can't easily review or clean up. The same principle applies to repo memory — NetPace's `CLAUDE.md` deprecates user-level memory and points to repo-tracked `.claude/memory/`.

**How to apply:** For any tool or skill that suggests `~/.claude/` or the system temp dir as a default scratch location (notably `/speckit.draftissue` and similar drafting workflows), override it to write into the repo root. The user can `.gitignore` the file or delete it after use. Default scratch location for this user: repo root. For memory entries specifically: write to `.claude/memory/` in the repo, not the user-level memory directory.
