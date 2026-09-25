# Change Intent Records (CIRs)

**Scope**: Documenting non-obvious development decisions
**Location**: `docs/change-intent-records/`
**Audience**: Future maintainers, AI agents, code reviewers

---

## What are Change Intent Records?

Change Intent Records capture the **why** behind non-obvious decisions made during development. They complement code comments (which explain "how") by documenting the reasoning, alternatives considered, and constraints that led to a particular implementation choice.

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

If any `→ CIR` row is ✅, write one.

## CIR Template

Save CIRs in `docs/change-intent-records/` as `YYYY-MM-DD-kebab-title.md`.

```markdown
# [Descriptive Title]

**Intent:** What was the goal or objective?

**Behaviour:** What are the expected outcomes? (given/when/then)

**Constraints:** What boundaries or guardrails applied?

**Decisions:** What alternatives were considered and rejected, and why?

**Date:** YYYY-MM-DD
```

When a record overturns part of an earlier one, add `**Supersedes:** <link> — <which decision>` so the older record is read in light of the newer.

For a worked example, see [`2026-09-03-whitespace-rules-match-stored-files.md`](../change-intent-records/2026-09-03-whitespace-rules-match-stored-files.md).

## References

- [Change Intent Records (Bryan Liles)](https://blog.bryanl.dev/posts/change-intent-records/)
- Architecture Decision Records (ADRs) - similar concept

---

**Last Updated**: April 2026
