---
name: Soft-wrap markdown — one line per paragraph, no hard breaks
description: Author markdown paragraphs as a single unwrapped line each; don't insert manual ~80/100-col hard line breaks. Reflowing wrapped prose rewrites whole blocks and buries a one-word edit in a noisy diff.
type: feedback
---

When writing or editing markdown in NetPace (docs, READMEs, memory files, PR bodies, issue comments), author each paragraph, list item, and table row as a **single unwrapped line**. Let the editor/viewer soft-wrap it. Do NOT insert manual hard line breaks to keep lines near 80 or 100 columns.

**Why:** Hard-wrapped paragraphs produce hostile diffs: changing one word near the start of a paragraph reflows every following line, so a one-word edit shows up as a whole rewritten block and the real change is impossible to spot in review. Soft-wrapped (one-line-per-paragraph) prose makes each edit a minimal, reviewable line-level change — the whole point of a text-based, diff-reviewed workflow.

**How to apply:**
- One physical line per paragraph. One physical line per bullet / numbered-list item. One physical line per table row. Blank line between paragraphs as usual.
- Do not "tidy" existing files by re-wrapping them at a column width, and do not add a hard-wrap step to any formatting hook or tooling.
- Genuine line breaks that are semantically meaningful (fenced code blocks, a deliberate two-space or backslash line break inside a paragraph) are unaffected — this is about not *reflowing prose to a column*.
- Adjacent guidance on keeping docs clean and diff-friendly: [[feedback_docs_no_forward_references]], [[feedback_no_spec_references]], [[feedback_no_column_alignment]].
