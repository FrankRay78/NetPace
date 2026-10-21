# Renaming the retired `ship` orchestrator to `/verify`, and cutting its pull-request step

**Intent:** Make the SDLC commands a chain of independently invocable stages — `/build` → `/verify` → `/raise-pr` — by cutting the pull-request step off the single orchestrator that previously did both jobs, and renaming what remains for what it actually guarantees. The retired name was `ship`.

**Behaviour:** `.claude/commands/verify.md` and `.claude/commands/raise-pr.md`; `docs/agentic-workflow.md` → *the verify gate*.

**Constraints:** Raising the pull request is the only irreversible, outward-facing act in the chain. Everything before it is safe to re-run; pushing a branch and opening a PR is not. That asymmetry is the whole reason the cut lands here and nowhere else.

**Decisions:**

- *Rejected — auto-invoking `/raise-pr` from `/verify` behind a flag.* It would have preserved the one-invocation convenience, and it is the option a future maintainer is most likely to reach for. It loses because the point of the split is that the outward-facing step is *always* deliberate: a flag reintroduces a path where a pull request is opened as a side effect of a verification run — and a default-off flag set once in a loop config is indistinguishable, at the call site, from the unsplit command.
- *Rejected — splitting `/verify` further into `/format` and `/review` stages.* The format → suite → review ordering is a hard structural gate, because review literally runs after the suite's exit code. Separating the stages would demote that structure to a convention a human has to remember in the right order.

**Date:** 2026-09-07
