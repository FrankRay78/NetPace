---
description: Scan the current conversation, and the branch's commit history, for durable learnings — corrections you gave Claude, questions that exposed an unclear artefact, rework the branch paid for, and written rules that produced the wrong answer. Applies approved ones, preferring a deterministic mechanism or a fix to the offending rule over a new memory entry.
---

Run this command at the end of a working session, after completing a feature, or after `/raise-pr`. It scans three independent sources — the conversation, the branch's commit history, and any posted PR review — then lists candidate learnings and applies the approved ones.

The order of preference for where a learning lands: **fix the rule that misfired** > **enforce it deterministically** > **write a memory entry**. Memory is the fallback, never the default: it costs context every session and only works when recall lands and Claude honours it.

## Steps

1. **Detect branch context (no hard gate — always proceed)**: The conversation scan is the primary source and runs unconditionally.

   Determine the **default branch** (first non-empty wins):
   - `git symbolic-ref refs/remotes/origin/HEAD --short 2>/dev/null | sed 's|^origin/||'`
   - `gh repo view --json defaultBranchRef --jq .defaultBranchRef.name 2>/dev/null`
   - fall back to `main`

   Then run `git rev-parse --abbrev-ref HEAD` for the **current branch**. Set `has_branch_context = true` when the current branch differs from the default *and* `git log <default>..HEAD --oneline` is non-empty; otherwise `has_branch_context = false`. Do not stop in any case — an empty branch context just means the conversation is the only source.

2. **Lens A — scan the conversation**: Read backwards through the conversation for each signal below. The first four are moments the user re-steered Claude; the last two are signals the user never phrased as a complaint at all, and are the ones a correction-only scan misses.

   **Explicit corrections** (Claude got something wrong): "no", "don't", "stop", "undo that", "that's wrong", "not like that", "remove that", "revert" — or output rejected and given a different direction.

   **Redirections** (scope or approach changed mid-flight): "actually", "instead", "wait", "hold on", "I meant", "let's do it differently".

   **Constraint reveals** (a rule or limitation Claude wasn't aware of): "we can't because", "remember we said", "that would break", "that's already handled by". Include **standing project facts stated in passing** — a posture on versioning, release cadence, risk appetite, or what the project does not care about yet. These are durable, rarely repeated, and a missed one costs every future session.

   **Confirmed non-obvious choices**: "yes exactly", "perfect", "keep doing that", "that's the right approach" — an unusual decision accepted without pushback.

   **Questions as defects**: every "why is this X?", "what does this actually mean?", "was this your idea or a requirement?" is evidence an artefact failed to explain itself. The defect is in the name, the comment, or the missing rationale — not in Claude's behaviour, which is why a correction-only scan scores these zero.

   **Reversals**: any position Claude stated and then abandoned. Separate *"I was wrong initially"* from *"I caved without new evidence"* — the second is its own failure mode and looks identical in a transcript.

3. **Lens B — mechanical evidence from git**: Only when `has_branch_context` is true. This lens needs no transcript and finds waste nobody complained about.

   **Churn** — regions the branch rewrote repeatedly:

   ```
   git log <default>..HEAD --name-only --pretty=format: -- src docs | grep -v "^$" | sort | uniq -c | sort -rn | head -12
   ```

   A file rewritten 3+ times in one branch means the earlier attempts were wrong. Ask *what question kept being re-answered there*. A cluster of sibling files that always move together is a structural signal: they share a shape that is not factored out.

   **Discarded work** — `git log <default>..HEAD --oneline --reverse`, looking for revert pairs and commits a later commit undoes. Each pair is a design decision made and unmade; the learning is what would have avoided the round trip.

   **Corroboration** — `git diff <default>...HEAD --name-status` for renames and moves, and any edits to `CLAUDE.md` or `.specify/memory/constitution.md`. A chat signal backed by a rework commit is higher confidence than either alone.

   When `has_branch_context` is false, skip this lens; the highest Confidence reachable in step 6 is then `Medium (chat only)`.

4. **Lens C — interpretation**: Apply these two questions to everything Lens A and Lens B surfaced. They produce the highest-value findings, and neither is visible in a raw transcript.

   **Rule collision** — did an *existing written rule* (`CLAUDE.md`, the constitution, an existing memory) produce the wrong answer here? If Claude followed a documented rule and the user overrode it, the defect is in the rule, not in Claude's recall. Adding a memory to counteract a miscalibrated rule leaves two conflicting instructions in context; amending the rule removes the conflict. Look for this explicitly — it is the most valuable finding the command can produce.

   **Who caught it** — for each defect found on the branch, ask who found it and how. A defect a human found *by eye* is a hole in the automated net, and names the mechanism that should have caught it. Watch especially for defects the test suite had **encoded** rather than caught: a wrong value baked into a snapshot or fixture passes forever.

   **Lens D — PR review**: unconditional, best-effort, independent of `has_branch_context`. If the branch has an open PR, `gh pr view --json comments,reviews` and route each substantive point through the same triage as Lenses A–C. A no-op when no review has posted — never wait or poll for one.

5. **Triage each signal by where it belongs**: four destinations, in order of preference.

   - **Amend an existing rule** — a documented rule misfired, is stale, or contradicts observed practice. Fix `CLAUDE.md`, the constitution, or the existing memory. Preferred over everything else: it removes a wrong instruction instead of adding a competing one.
   - **Deterministic mechanism** — a mechanically checkable rule (a path must or must not exist, a command must run before/after X, a file must match a shape, a value must be set, an invariant must hold). Automated behaviours ("whenever X, do Y") *require* a hook in `settings.json` — the harness executes those, not Claude, so a memory entry cannot fulfil them; the `update-config` skill wires hooks, permissions, and env. A recurring code-shape or data-invariant violation → an architectural or unit test.
   - **Memory entry** — judgment, taste, or context ("prefer X when Y", "explain tradeoffs plainly"), and standing project facts. No mechanism captures these; forcing them into a hook is worse. A **structural decision** (a shape not yet factored out, surfaced by Lens B's churn/sibling check) defaults here too — as `.claude/memory/project_*.md` — unless it turns out to be a fix to a stale documented rule, in which case route it to Amend rule instead.
   - **Both** — a checkable rule with non-obvious rationale: the mechanism enforces the *what*, a short memory records the *why*.

6. **Synthesise candidates**: Scale the cap to the session — **at most 5** for a short session, **up to 8** when the branch carries many commits or the PR many review threads. Fewer is always fine. Only include signals that are non-obvious, generalisable, and likely to recur; skip anything one-off, feature-specific, or already documented.

   For each candidate write:

   ```
   ### [N]. [Short title — 8 words or fewer]
   Evidence: [the conversation moment, churn or revert pair, rule collision, or PR review point that triggered this]
   Lens: A (conversation) | B (git) | C (interpretation) | D (PR review)
   Confidence: High (two sources) | Medium (chat only) | Low (git only)
   Category: Claude correction | Process gotcha | Structural decision | Confirmed approach | Miscalibrated rule
   Destination: Amend rule | Deterministic | Memory | Both (from the step 5 triage)
   Draft: [one sentence. For Amend rule, quote the offending text and the proposed replacement. For Deterministic, name the mechanism.]
   ```

7. **Confirm with the user**: After listing the candidates, ask:

   > "Which of these are worth keeping? Reply with the numbers (e.g. `1 3`), or `none`."

   Wait for the reply before writing anything.

8. **Check for duplicates — and for invalidation**: For each approved candidate, grep `.claude/memory/` for related terms; if a closely related memory exists, *update* it rather than creating a near-twin.

   Then run the reverse check: did anything this session make an **existing** memory or documented rule stale, wrong, or redundant? A memory superseded by a constitution rule, or one describing code that no longer exists, should be amended or deleted. Carrying a wrong memory is worse than carrying none.

9. **Write approved learnings**:

   **Amend rule** — show the exact before/after for the offending line and apply on approval. An amendment to `.specify/memory/constitution.md` needs a version bump and a `Last Amended` date per its own Governance section, whose step 4 also requires noting which downstream documents (`CLAUDE.md`, `docs/conventions/`) were reviewed in lockstep.

   **Deterministic** — these change how the harness or test suite behaves, so **propose, then apply only on explicit approval**; never silently edit `settings.json`. For a hook, permission, or env change, invoke the `update-config` skill with the concrete rule. For a code-shape or invariant rule, add the test following `CLAUDE.md` and the constitution's Testing Standards — including a RED run proving it fails on the violation.

   **Memory** — write (or update) a file in `.claude/memory/`:

   ```markdown
   ---
   name: [title]
   description: [one-line description for relevance matching]
   type: feedback | project
   ---

   [Draft from step 6]

   **Why:** [the conversation moment, rework, or constraint that caused it]
   **How to apply:** [when this should influence future behaviour]
   ```

   Link related entries with `[[name]]`. For a **Both** candidate, point the memory's **How to apply** at the enforcing mechanism. Then add a pointer to `.claude/memory/MEMORY.md` — one flat list, one line per entry, under ~150 characters, format `- [Title](file.md) — one-line hook`. No section headings; the list is not categorised.

10. **Output result**: List rules amended, mechanisms applied or proposed, memory entries added or updated, and anything deleted as stale. If nothing was approved, output: "No learnings captured."
