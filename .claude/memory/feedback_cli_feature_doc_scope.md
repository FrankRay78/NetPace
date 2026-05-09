---
name: NetPace CLI feature issues must scope user-facing docs from the start
description: When drafting issues for NetPace CLI features, include user-facing docs (README --help snapshot, USER_GUIDE, design-doc cross-refs) in acceptance criteria, not just XML docs and CIR.
type: feedback
---

When drafting an issue or spec for a NetPace CLI feature, include user-facing documentation requirements in the acceptance criteria from the *first* draft — not just XML docs and a CIR. The full set: README `--help` output snapshot, USER_GUIDE section, and design-doc cross-references where the feature touches an architecture doc.

**Why:** During issue #174 drafting, Claude's first draft only listed XML docs and a CIR under documentation. User had to prompt: "Have you made a note that some user documentation will need to be generated too?" — at which point the broader docs surface (README `--help`, USER_GUIDE section, design-doc cross-ref) was added. NetPace's `CLAUDE.md` paired-rules section is the source of truth for what to include and was missed. Note: per-release "what changed" notes are GitHub-auto-generated from merged PRs (and `<PackageReleaseNotes>` already points to the Releases page) — there is intentionally no CHANGELOG.md to maintain; do not add one back into acceptance criteria.

**How to apply:** For any `/speckit.draftissue`, `/speckit.specify`, or general issue-drafting work that introduces or modifies a CLI option/flag/subcommand in NetPace, the acceptance-criteria documentation block must include all of: (1) README.md `--help` snapshot refresh, (2) USER_GUIDE.md section update, (3) design-doc cross-ref where applicable, (4) XML docs on all new public APIs, (5) CIR for any public-API surface change. Don't wait for the user to remind you.
