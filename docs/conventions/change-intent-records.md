# Change Intent Records (CIRs)

**Scope**: Documenting non-obvious development decisions
**Location**: `docs/change-intent-records/`
**Audience**: Future maintainers, AI agents, code reviewers

---

## What are Change Intent Records?

Change Intent Records capture the **why** behind non-obvious decisions made during development. They complement code comments (which explain "how") by documenting the reasoning, alternatives considered, and constraints that led to a particular implementation choice.

A record is also the one place in the codebase where a GitHub issue number belongs. The only exception is a regression test or acceptance criterion naming the bug it guards against (Constitution §IX's *Regression exception*, worked in [`testing.md`](testing.md)). Everywhere else the reasoning stays as prose and the number goes, because an issue thread cited for its reasoning is standing in for a record that was never written — write the record and link that instead.

**Decision table** — does this change need a CIR?

| Question | → CIR | → No CIR |
| --- | --- | --- |
| Choosing between multiple viable approaches a future maintainer would reasonably question? | ✅ | |
| Working around a limitation or constraint the code itself won't make obvious? | ✅ | |
| Architectural decision that constrains future work (public API, dependency direction, framework choice)? | ✅ | |
| Trade-off made (performance vs. readability, simplicity vs. flexibility) where the rejected option is plausible? | ✅ | |
| Following an established pattern already documented elsewhere in the repo? | | ✅ |
| Obvious or standard implementation with no real alternative? | | ✅ |
| Temporary workaround you expect to remove? | | ✅ (code comment + TODO instead) |
| Reasoning already recorded in the issue's confirmed decisions? | | ✅ (link the issue from the commit message or the pull request body rather than restating it. This does not cover a decision that constrains future work — a public API, an interface contract, dependency direction — which still gets a short record linking the issue for its reasoning) |
| Change only to documentation written for the project's readers, such as README.md or USER_GUIDE.md? | | ✅ (command prompts, conventions, the constitution and workflows direct future work and are not covered by this row) |

Write a record when a `→ CIR` row applies **and** no `→ No CIR` row does. Most changes need none.

The confirmed-decisions row carries that exception because an issue number may not be written in the codebase outside a record (see above, and `CLAUDE.md`'s paired rule). A decision that constrains future work and has no record therefore leaves the code nowhere to point for its rationale.

## CIR Template

Save CIRs in `docs/change-intent-records/` as `YYYY-MM-DD-kebab-title.md`.

```markdown
# [Descriptive Title]

**Intent:** What was the goal or objective?

**Behaviour:** The expected outcome in a sentence or two — or a one-line pointer where the issue, a prompt or a doc already specifies it.

**Constraints:** What boundaries or guardrails applied?

**Decisions:** What alternatives were considered and rejected, and why?

**Date:** YYYY-MM-DD
```

**Target about 150 words.** A record states its intent in a sentence, and then keeps only what is written nowhere else: each rejected alternative with the reason it was rejected, and any constraint the code will not make obvious. Everything else is linked rather than restated — behaviour to the issue, review notes to the pull request, a documented pattern to the doc that carries it. Write one-line bullets, not paragraphs.

Each piece of reasoning is written once. A rejected alternative belongs in the record, so where a record exists the pull request body points to it instead of repeating its reasoning. A read of ten records found roughly one word in five written nowhere else, against a median length of 464 words; one record was first written at 39 lines, most of it duplicating `verify.md` and the issue, and had to be trimmed in review.

When a record overturns part of an earlier one, add `**Supersedes:** <link> — <which decision>` so the older record is read in light of the newer.

**A worked sample at the target length.** This is the whitespace-rules decision — a real record, written to this guidance rather than at the length it was first filed:

```markdown
# Whitespace Rules Must Agree With What Is Stored

**Intent:** Stop a formatting rule asserting something the stored files contradict, so a clean checkout stops reporting files as modified.

**Behaviour:** See the issue. Gated by `dotnet format --verify-no-changes` over `src/NetPace.sln` in CI.

**Constraints:** Verify snapshots (`*.verified.*`) are byte-exact program output — their trailing whitespace and missing final newlines are content, not formatting — so the rules must exempt them.

**Decisions:**

- `insert_final_newline = true`. Rejected: keeping `false` and stripping the files that already end with one. It satisfies the rule equally well, but leaves `\ No newline at end of file` in every diff touching a last line.
- `* text=auto eol=lf` as a wildcard. Rejected: enumerating each extension — an enumeration covers only what someone remembered to add, so a file type introduced later falls through silently.
- The formatter's own check as the gate. Rejected: hygiene tests parsing `.editorconfig` themselves — 600 lines covering three of its keys, reporting success whenever their own matching failed.

**Date:** 2026-09-03
```

## References

- [Change Intent Records (Bryan Liles)](https://blog.bryanl.dev/posts/change-intent-records/)
- Architecture Decision Records (ADRs) - similar concept

---

**Last Updated**: October 2026
