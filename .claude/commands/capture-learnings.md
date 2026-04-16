---
description: After a PR is raised, scan the branch history for signals of corrections, decisions, and gotchas — then capture the ones worth keeping as memory entries.
---

Run this command at the end of a feature, after `/raise-pr`. It reads the git history and diff to find evidence of what went wrong or was decided during the feature, surfaces a short list of candidates, and writes the ones you approve to the memory system.

## Steps

1. **Guard**: Run `git rev-parse --abbrev-ref HEAD`. If the result is `main`, stop and output: "Run /capture-learnings from a feature branch, not main." Run `git log main..HEAD --oneline`. If empty, stop and output: "No commits on this branch — nothing to scan."

2. **Gather evidence**: Run both of these:
   - `git log main..HEAD --oneline --reverse` — full commit list, oldest first
   - `git diff main...HEAD --name-status` — which files changed and how (A=added, M=modified, R=renamed/moved, D=deleted)

3. **Detect signals**: Scan the commit messages and file changes for the following signal types. A single commit or file change may yield more than one signal.

   **Claude behavior signals** (things Claude did wrong that required correction):
   - Commit messages containing: "slop", "remove ai", "tune", "token", "post review fix", "post implementation review", "review comments", "feedback fixed", "feedback"
   - Multiple rework commits on the same area after implementation

   **Process gotcha signals** (constraints or workflow rules discovered mid-flight):
   - Commit messages containing: "don't", "bug fix", "fix" on files in `scripts/`
   - New files added under `scripts/` mid-branch (signals new tooling was needed)

   **Structural decision signals** (conventions or file locations that turned out to be wrong):
   - Renamed or moved files (R status) — especially CLAUDE.md, docs files, or config files
   - Modifications to `CLAUDE.md`, `.specify/memory/constitution.md`, or `docs/agentic-software-development-workflow.md`

4. **Synthesize candidates**: Produce at most 5 candidates. Fewer is fine — only include signals that are non-obvious or likely to recur. Skip signals that are clearly one-off, feature-specific, or already documented. For each candidate write:

   ```
   ### [N]. [Short title — ≤8 words]
   Evidence: [commit message(s) or file change that triggered this]
   Category: Claude behavior | Process gotcha | Structural decision
   Target: [see mapping below]
   Draft: [one sentence suitable for a memory entry]
   ```

   **Target mapping:**
   - Claude behavior → `.claude/memory/feedback_*.md`
   - Process gotcha → `.claude/memory/feedback_*.md`, or note a suggested CLAUDE.md edit
   - Structural decision → `.claude/memory/project_*.md`, or note a suggested CLAUDE.md / workflow doc edit

5. **Confirm with the user**: After listing the candidates, ask:
   > "Which of these are worth keeping? Reply with the numbers (e.g. `1 3`), or `none`."

   Wait for the reply before writing anything.

6. **Write approved learnings**: For each approved candidate, write a memory file to `.claude/memory/` using this format:

   ```markdown
   ---
   name: [title]
   description: [one-line description for relevance matching]
   type: feedback | project
   ---

   [Draft from step 4]

   **Why:** [inferred from the evidence]
   **How to apply:** [when this should influence future behaviour]
   ```

   Then add a pointer line to `.claude/memory/MEMORY.md` (create it if it does not exist, with a `# Learnings` heading).

   If the candidate's target was a CLAUDE.md or workflow doc edit rather than a memory file, describe the suggested edit instead and ask the user whether to apply it.

7. **Output result**: List the files written (or edits proposed). If nothing was approved, output: "No learnings captured."
