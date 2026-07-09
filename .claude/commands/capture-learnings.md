---
description: Scan the current conversation (and, on a feature branch, the commits since branching) for corrections and redirections you gave Claude — then capture the ones worth keeping, preferring deterministic enforcement (hooks, tests, settings) over a memory entry wherever the learning can be mechanically enforced.
---

Run this command at the end of a working session, after completing a feature, or after `/raise-pr`. It always scans the current conversation for moments where you corrected or re-steered Claude; if you're on a non-default branch, it also looks at commits since the branch point as a secondary corroboration source. It then asks a few discovery questions, lists candidate learnings, and applies approved ones — preferring a deterministic mechanism (hook, test, or `settings.json` change) whenever the learning is mechanically enforceable, and falling back to a memory entry only for judgment or context no mechanism can capture.

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

4. **Prefer deterministic enforcement over a memory entry, where the learning allows it**: A memory entry is a *rule Claude must remember* — it costs context every session and only works when recall lands and Claude honours it. A hook, `settings.json` change, lint rule, or architectural/unit test enforces the same learning *deterministically*: zero tokens, cannot be forgotten, fails loud. NetPace already leans this way — the constitution makes TDD non-negotiable so the test suite (not a reminder) is what stops regressions, and speckit git hooks are wired in `settings.json` rather than left to Claude to remember.

   This triage by **enforceability** is the lens step 5 applies to each candidate before assigning its target — three classes:
   - **Mechanically checkable rule** (a path must/mustn't exist, a command must run before/after X, a file must match a shape, a value must be set) → prefer a deterministic mechanism. Automated behaviours ("whenever X, do Y") require a hook in `settings.json` — the harness executes those, not Claude, so a memory entry *cannot* fulfil them; the `update-config` skill wires hooks, permissions, and env. A recurring code-shape violation → an architectural or unit test.
   - **Judgment, taste, or context** ("prefer X when Y", "explain tradeoffs plainly") → a memory entry, as before. No mechanism can capture these; forcing them into a hook is worse.
   - **A checkable rule with non-obvious rationale** → **both**: the mechanism enforces the *what*, a short memory records the *why*.

   Memory is the explicit fallback, not the default. Only route to memory once you've ruled out a deterministic mechanism.

5. **Synthesise candidates**: Produce at most 5 candidates. Fewer is fine — only include signals that are non-obvious, generalisable, and likely to recur. Skip signals that are clearly one-off, feature-specific, or already documented.

   For each candidate write:

   ```
   ### [N]. [Short title — ≤8 words]
   Evidence: [the conversation moment(s) or git change that triggered this]
   Confidence: High (chat + git) | Medium (chat only) | Low (git only)
   Category: Claude correction | Process gotcha | Structural decision | Confirmed approach
   Enforceability: Deterministic | Judgment | Both (from the step 4 triage)
   Target: [see mapping below]
   Draft: [one sentence suitable for a memory entry — and, for Deterministic/Both, name the mechanism (e.g. "PreToolUse hook blocking git-add of .claude/scratch/", "xUnit test asserting no [Fact(Skip)]")]
   ```

   **Target mapping** (route by the Enforceability triage from step 4 first, then by Category):
   - Deterministic mechanism → a hook / permission / env change in `settings.json` (via the `update-config` skill), or a lint / architectural / unit test. Preferred whenever the learning is mechanically checkable.
   - Claude correction (judgment) → `.claude/memory/feedback_*.md`
   - Process gotcha → a deterministic mechanism if checkable; otherwise `.claude/memory/feedback_*.md`, or a suggested CLAUDE.md edit
   - Structural decision → `.claude/memory/project_*.md`, or a suggested CLAUDE.md edit
   - Confirmed approach → `.claude/memory/feedback_*.md`
   - Both → the mechanism above **plus** a short memory entry recording the rationale

6. **Confirm with the user**: After listing the candidates, ask:

   > "Which of these are worth keeping? Reply with the numbers (e.g. `1 3`), or `none`."

   Wait for the reply before writing anything.

7. **Check for duplicates**: For each approved candidate, grep `.claude/memory/` for related terms. If a closely related memory already exists, note whether this should *update* the existing file rather than create a new one.

8. **Write approved learnings**: Apply each approved candidate to the target its Enforceability triage chose.

   **Deterministic targets** (hook / permission / env / test) — these change how the harness or test suite behaves, so **propose, then apply only on explicit approval**; never silently edit `settings.json`:
   - For a hook / permission / env change, invoke the `update-config` skill with the concrete rule (e.g. "PreToolUse hook that rejects `git add` of paths under `.claude/scratch/`") and let it wire `settings.json`.
   - For a code-shape rule, add the architectural or unit test that fails on violation, following the testing conventions in `CLAUDE.md` and the constitution's Testing Standards.
   - For a **Both** candidate, apply the mechanism *and* write the memory entry below, with the memory's **How to apply** pointing at the enforcing mechanism.

   **Memory targets** — write (or update) a memory file in `.claude/memory/` using this format:

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

   Then add a pointer line to `.claude/memory/MEMORY.md` — one flat list, one line per entry, under ~150 characters, format `- [Title](file.md) — one-line hook`. No section headings; the list is not categorised.

   If the candidate's target was a CLAUDE.md edit rather than a memory file, describe the suggested edit and ask the user whether to apply it.

9. **Output result**: List the files written or updated, the memory entries added, and any deterministic mechanisms applied or proposed (hooks, tests, settings edits awaiting approval). If nothing was approved, output: "No learnings captured."
