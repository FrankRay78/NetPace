# Harness commands keep project specifics in one section

**Intent:** Let the `/build` → `/verify` chain be adopted in another repository by rewriting one section per command rather than hunting NetPace details through every step. Refs #424.

**Behaviour:** `/build` and `/verify` each end with a *Project-specific* section holding every NetPace binding their steps use; the steps themselves name no NetPace tool, path, product rule or constitution section. `CLAUDE.md`'s paired rules carry the standing convention. Behaviour on NetPace is unchanged, and the verdict and reason strings the chain reads are byte-identical.

**Constraints:**
- Both commands are in unattended production use, so the move could not alter what either does on NetPace.
- The verdict-line protocol is the chain's own contract (`scripts/chain.sh` reads the last line), so the forms and the fixed formatter-failure reason had to survive the move verbatim — the steps name them indirectly, the project section states them.

**Decisions:** what stays in the steps, and why, since this is the line a future editor has to draw again:
- **Stays — repo-agnostic.** `main` as the default branch; the slash-command names, which are a harness that hangs together rather than NetPace features; the `pr-review-toolkit` reviewer names, which belong to a portable plugin; branch naming and the `Refs #<N>` reference form, because `/raise-pr` derives the issue link from them; illustrative issue numbers in example commit messages, which the issue-number rule already exempts.
- **Moves — project binding.** The build, test and format commands and the reasoning attached to their form; the project-context file; the test conventions; the skipped-test ban and the stale-build ban, with the hooks that enforce them; the protected public API, the dependency-controlled library and the pre-1.0 stance; the documentation obligations; where the constitution states TDD and traceability; the chain driver and traceability script names; the doc holding the formatting-cadence reasoning; Review B's workflow and author gate.
- **Rejected: listing the default branch as a port assumption.** `main` is treated as repo-agnostic — the commands will only be ported to repositories that use it — so neither section mentions it.
- **Rejected: replacing the removed evidence citations with links to this record.** Each command is self-contained; a link from step text to a record or an issue is historic rot. The citations were deleted, and the claims they supported stand as prose.
- **Rejected: a *Project-specific* section in `/review-slop`.** It was checked and holds nothing repository-specific — the "against main" diff base and the type-system-workaround example are both repo-agnostic — so it is unchanged, per the convention's "when next edited" reach.
- **Rejected: a CI gate asserting the steps stay generic.** The check is a word blocklist that has to parse the section boundary; it would be brittle, false-positive on the project section's legitimate uses, and larger than the rule it guards. The convention in `CLAUDE.md` carries it instead.

**Date:** 2026-10-20
