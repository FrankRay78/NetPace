# One Table For Stage Models

**Intent:** Every stage a runner starts unattended runs on a model and reasoning effort chosen on purpose, written in one place a person can read, rather than scattered across workflows and the chain — and in three workflows not written at all, so they ran on whatever the action defaulted to.

**Behaviour:** [`scripts/stage-models.tsv`](../../scripts/stage-models.tsv) holds one row per stage: its model and effort. `scripts/stage-flags.sh <stage>` turns a row into the flags. `scripts/chain.sh` reads all three of its stages' rows before the first starts; each Claude workflow looks its stage up in a step before the Claude step. `scripts/stage-flags.tests.sh` fails if a runner names a stage the table lacks, or writes a model or effort of its own.

**Constraints:**

- A command run by hand keeps the session's model. The table is read only by runners, so choosing a model at the keyboard still works.
- A model a command picks for its own subagents — review-pr's triage map on Haiku — stays in the command. It is part of how the command works, so it holds however the command was started.

**Decisions:**

- Split by who decides. The runner decides the stage's model, the command decides its subagents'. That split is what lets manual runs be left alone while unattended ones are pinned.
- Rejected `model:` frontmatter on each command. It would sit beside the prompt, but it also applies to a hand run, the opposite of the first constraint.
- Rejected GitHub repository variables. Not visible in the code, and the chain cannot read them.
- A stage with no row, or a row missing its effort, fails rather than falls back. A silent default is the problem the table exists to remove.
- The two PR-triggered workflows read main's copy of the table and the lookup, not the PR head's, on the same untrusted-head reasoning as [`2026-10-28-review-rounds-record-what-they-cost`](2026-10-28-review-rounds-record-what-they-cost.md). Accepted: a change to the table reaches those reviews only on merge, and the PR that introduces the table cannot be reviewed by them until it is on main.
- `CHAIN_MODEL` stays as an override of every chain stage's model, for a one-off run; the effort stays the table's.

**Date:** 2026-10-29
