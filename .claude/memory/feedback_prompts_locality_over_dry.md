---
name: Prompts favour locality over DRY
description: For LLM-consumed prompt files, repeat short rules inline rather than centralising with cross-references; don't defensively over-specify cases the controlled generator can't produce
type: feedback
---

When editing the `.md` prompt files under `.claude/commands/` (and similar prompts loaded as slash commands), prefer **locality over DRY**:

- Each prompt file should state the rules it needs **inline** and self-contained.
- Avoid central "canonical spec" sections referenced via "see X for full rules" pointers, unless the rule is genuinely complex (multi-paragraph, sub-grammar, etc.).
- For a 3-line rule, three short inline copies in three files is fine — even preferable.

Also avoid **defensive over-specification** of cases the controlled producer can't emit. Where one prompt writes both ends of a convention — an issue's `**Scenario:**` label and the test's matching `// SCENARIO:` marker — defending a reader against `///`, `/* SCENARIO: */`, NBSP, NFD-normalised text, etc. is YAGNI because nothing on that path produces them.

**Why:** when a slash command runs, the LLM loads only that command's prompt — not other prompts it might cross-reference. Locality means the rule is in front of the agent at the moment it acts. Centralisation requires the agent to chase a link, which it may skip if the inline statement looks complete. The DRY win (single source of truth, no drift risk) only pays off when the rule is large enough that paraphrases would actually drift.

**How to apply:**
- When tempted to add a "Match contract" / "Spec" / "Rules" section in one file with cross-references in others, ask: is the rule >5 lines? If no, inline it everywhere.
- When tempted to enumerate negative cases (rejected prefixes, malformed input, exotic Unicode), ask: can the generator produce this? If no, drop it.
- Three short repeats of the same rule is acceptable — diff churn from a future rule change is far cheaper than confused agents at runtime.

Origin: 2026-05-10 review of the AC-to-test traceability branch — initially added a 13-line `### Match contract` block + cross-references in two consumer files. User pushed back on the over-engineering; flattened to inline-everywhere with +3 words net.
