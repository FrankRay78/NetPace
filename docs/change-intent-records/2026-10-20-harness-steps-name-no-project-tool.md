# Harness steps name no project tool, even in a fixed reason

**Supersedes:** [2026-10-20-harness-commands-keep-specifics-in-one-section](2026-10-20-harness-commands-keep-specifics-in-one-section.md) — keeping `dotnet format failed` and the `agentic-workflow.md` link in `/verify`'s steps.

**Intent:** Let `/build` and `/verify` be adopted elsewhere by rewriting *Project-specific* alone, as that record promised.

**Behaviour:** `/verify`'s formatter failure reports `FAILED reason=formatting failed`; step 1a no longer links the workflow guide; `/build` step 9 writes no record where the project keeps none.

**Decisions:**

- Generic reason wording. Rejected: keeping `dotnet format failed` as a protocol token — `scripts/chain.sh` reads only the `FAILED reason=` shape and relays the text, so the tool name protected nothing and forced a port to edit four step lines. Rejected: moving the wording to *Project-specific* — one generic phrase needs no per-project binding.
- No link from a command to `docs/agentic-workflow.md`. The guide describes the commands; a command pointing back at it is a dependency a port must carry for no behaviour.

**Date:** 2026-10-20
