---
description: Audit CLAUDE.md, its linked docs, and .claude/memory/ against the patterns from "A good AGENTS.md is a model upgrade" — flag bloat, stale references, mis-fit patterns, and verbose passages, then apply approved edits.
reference: https://www.augmentcode.com/blog/how-to-write-good-agents-dot-md-files
---

Run this command quarterly, after a big architectural shift, or whenever `CLAUDE.md` feels stale. It treats `CLAUDE.md` + the docs it links to + `.claude/memory/` as a garden to prune and reshape — not just a doc to update. Different from `/capture-learnings`, which *grows* memory from a session; this one *prunes and reshapes* what is already there.

The audit is advisory: candidates are flagged with rationale, you pick which to apply.

## Steps

1. **Discover scope**: Build the list of files to audit:
   - `CLAUDE.md` (project root)
   - Every relative-path link inside `CLAUDE.md` that resolves to a file in this repo (e.g. `.specify/memory/constitution.md`, `docs/conventions/*.md`, `docs/architecture/*.md`)
   - Every file under `.claude/memory/` (including `MEMORY.md`)

   Skip URLs and files outside the repo. Record the list — every later check runs against each file.

2. **Run the seven checks**: For each file, evaluate:

   **Check 1 — Length / bloat**: Count lines. Flag `CLAUDE.md` sections that push the file past ~150 lines (the article's threshold where gains reverse). Suggest promoting verbose blocks to a reference file.

   **Check 2 — Pattern fit**: For each section, ask which pattern it *should* be and flag mismatches:
   - Gotchas / sharp edges → "Don't" paired with "Do"
   - Two-or-three-way ambiguity → decision table
   - Multi-step wiring → numbered procedural workflow
   - Convention enforcement → 3–10 line real-codebase example
   - Memory `feedback_*.md` entries → rule + **Why:** + **How to apply:** structure

   **Check 3 — Generic best practices**: Flag sections that read like generic software advice with no NetPace, C#, or repo-specific anchor. Generic advice doesn't change agent behaviour — propose deletion.

   **Check 4 — Stale references**: For every file path, function name, flag, or symbol named in the doc:
   - Verify the file/path still exists (`ls`)
   - Verify the symbol still exists (`grep`)
   Flag every miss. Memory entries are particularly prone to this — the auto-memory rules already warn about it.

   **Check 5 — Pattern drift**: Run `git log --oneline -50` and skim recent commits. Flag any section describing "how we do X" where recent commits show X is no longer done that way (e.g. renamed module, replaced library, abandoned convention).

   **Check 6 — Reference fan-out**: Count outbound links per file. Warn if `CLAUDE.md` exceeds ~10–15 (the article's threshold) — the agent will read all of them and drift.

   **Check 7 — Verbose explanation**: Flag passages that could be trimmed with no material loss of intention. Look for:
   - Restating the same point in two ways
   - Long preambles before the actual rule
   - Adjectives and qualifiers that don't change behaviour
   - Examples that duplicate what the rule already states

   For each, draft the tightened version so the user can compare side-by-side.

3. **Synthesise candidates**: Produce a numbered list. Each candidate:

   ```
   ### [N]. [File] — [Short title — ≤8 words]
   Check: [1–7]
   Issue: [one sentence]
   Suggested action: prune | promote to reference | convert to <pattern> | refresh | tighten | delete
   Draft: [the proposed replacement, or "(delete)"]
   ```

   Group by file so the user can see all proposals for one file together. Cap the list at ~15 candidates per run; if there are more, surface the highest-confidence ones and note how many were held back.

4. **Confirm with the user**: After listing candidates, ask:

   > "Which of these should I apply? Reply with the numbers (e.g. `1 3 7`), `all`, or `none`."

   Wait for the reply. Do not write anything before the reply.

5. **Apply approved edits**: For each approved candidate:
   - Use the `Edit` tool against the target file with the drafted replacement
   - For "promote to reference" actions, create the new reference file under `docs/` and replace the original block with a one-line link
   - For memory entries flagged for deletion, remove both the file and its `MEMORY.md` pointer

   After all edits, run `git diff --stat` so the user can see the footprint of the changes.

6. **Output result**: List the files changed and the candidates that were applied vs skipped. If nothing was approved, output: "No gardening applied."
