---
description: Scan the current conversation (and, on a feature branch, the commits since branching) for corrections and redirections you gave Claude — then capture the ones worth keeping as memory entries.
---

Run this command at the end of a working session, after completing a feature, or after `/raise-pr`. It always scans the current conversation for moments where you corrected or re-steered Claude; if you're on a non-default branch, it also looks at commits since the branch point as a secondary corroboration source. It then asks a few discovery questions, lists candidate learnings, and writes approved ones to the memory system.

## Steps

1. **Detect branch context (no hard gate — always proceed)**: The conversation scan is the primary source and runs unconditionally.

   Determine the **default branch** (first non-empty wins):
   - `git symbolic-ref refs/remotes/origin/HEAD --short 2>/dev/null | sed 's|^origin/||'`
   - `gh repo view --json defaultBranchRef --jq .defaultBranchRef.name 2>/dev/null`
   - fall back to `main`

   Then run `git rev-parse --abbrev-ref HEAD` for the **current branch**. Set `has_branch_context = true` when the current branch differs from the default *and* `git log <default>..HEAD --oneline` is non-empty; otherwise `has_branch_context = false`. Do not stop in any case — an empty branch context just means the conversation is the only source.

2. **Scan conversation for correction signals**: Read backwards through the current conversation looking for moments where the user corrected, redirected, or re-steered Claude. Classify each as one of:

   **Explicit corrections** (Claude got something wrong):
   - User said: "no", "don't", "stop", "undo that", "that's wrong", "not like that", "remove that", "revert"
   - User rejected output and gave a different direction

   **Redirections** (scope or approach changed mid-flight):
   - User said: "actually", "instead", "wait", "hold on", "I meant", "let's do it differently"
   - User changed requirements or constraints partway through

   **Constraint reveals** (a rule or limitation Claude wasn't aware of):
   - User said: "we can't because", "remember we said", "that would break", "that's already handled by"
   - A previously undisclosed constraint surfaced

   **Confirmed non-obvious choices** (user validated something Claude might not repeat):
   - User said: "yes exactly", "perfect", "keep doing that", "that's the right approach"
   - User accepted an unusual or counter-intuitive decision without pushback

3. **Gather git corroboration** (secondary source — only when `has_branch_context` is true): Run:
   - `git log <default>..HEAD --oneline --reverse`
   - `git diff <default>...HEAD --name-status`

   Use this to *corroborate* conversation signals, not generate new ones. A chat correction backed by a rework commit is a higher-confidence candidate. Also check for:
   - Renamed or moved files (R status) — especially CLAUDE.md, docs, or config files
   - Modifications to `CLAUDE.md` or `.specify/memory/constitution.md`

   When `has_branch_context` is false (running on the default branch, or feature branch with no commits yet), skip this step — only Confidence levels reachable in step 5 are `Medium (chat only)`.

4. **Ask discovery questions**: Before synthesising candidates, ask the user:

   > "Before I write up candidates — was there anything surprising, a wrong assumption I made, or something that took longer than expected that didn't show up clearly in our conversation?"

   Wait for a reply. Incorporate any freeform input as additional candidates.

5. **Synthesise candidates**: Produce at most 5 candidates. Fewer is fine — only include signals that are non-obvious, generalisable, and likely to recur. Skip signals that are clearly one-off, feature-specific, or already documented.

   For each candidate write:

   ```
   ### [N]. [Short title — ≤8 words]
   Evidence: [the conversation moment(s) or git change that triggered this]
   Confidence: High (chat + git) | Medium (chat only) | Low (git only)
   Category: Claude correction | Process gotcha | Structural decision | Confirmed approach
   Target: [see mapping below]
   Draft: [one sentence suitable for a memory entry]
   ```

   **Target mapping:**
   - Claude correction → `.claude/memory/feedback_*.md`
   - Process gotcha → `.claude/memory/feedback_*.md`, or note a suggested CLAUDE.md edit
   - Structural decision → `.claude/memory/project_*.md`, or note a suggested CLAUDE.md edit
   - Confirmed approach → `.claude/memory/feedback_*.md`

6. **Confirm with the user**: After listing the candidates, ask:

   > "Which of these are worth keeping? Reply with the numbers (e.g. `1 3`), or `none`."

   Wait for the reply before writing anything.

7. **Check for duplicates**: For each approved candidate, grep `.claude/memory/` for related terms. If a closely related memory already exists, note whether this should *update* the existing file rather than create a new one.

8. **Write approved learnings**: For each approved candidate, write (or update) a memory file in `.claude/memory/` using this format:

   ```markdown
   ---
   name: [title]
   description: [one-line description for relevance matching]
   type: feedback | project
   ---

   [Draft from step 5]

   **Why:** [inferred from the evidence — the conversation moment or constraint that caused it]
   **How to apply:** [when this should influence future behaviour]
   ```

   Then add or update a pointer line in `.claude/memory/MEMORY.md` (create it if it does not exist, with a `# Learnings` heading). Each pointer: one line, under ~150 characters, format `- [Title](file.md) — one-line hook`.

   If the candidate's target was a CLAUDE.md edit rather than a memory file, describe the suggested edit and ask the user whether to apply it.

9. **Output result**: List the files written or updated (or edits proposed). If nothing was approved, output: "No learnings captured."
