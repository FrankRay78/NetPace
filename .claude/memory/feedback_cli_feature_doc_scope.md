---
name: NetPace CLI feature issues must scope user-facing docs from the start
description: When drafting an issue that adds or changes a CLI option/flag/subcommand, list the user-facing docs in the first draft's ACs.
type: feedback
---

When drafting an issue (`/speckit.draftissue` or by hand) that adds or changes a CLI option, flag or subcommand, the first draft's docs block must include everything in CLAUDE.md's CLI-option rule (README `--help` snapshot, USER_GUIDE, design-doc cross-ref), plus XML docs and a CIR for any public-API change.

**Why:** In the #174 draft, only XML docs and a CIR were listed, and Frank had to prompt for the user docs.

**How to apply:** Check the docs block against the CLAUDE.md rule before showing the draft. No CHANGELOG entry.
