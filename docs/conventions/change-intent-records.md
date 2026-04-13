# Change Intent Records (CIRs)

**Scope**: Documenting non-obvious development decisions
**Location**: `docs/change-intent-records/`
**Audience**: Future maintainers, AI agents, code reviewers

---

## What are Change Intent Records?

Change Intent Records capture the **why** behind non-obvious decisions made during development. They complement code comments (which explain "how") by documenting the reasoning, alternatives considered, and constraints that led to a particular implementation choice.

**When to Create a CIR**:
- When you choose between multiple viable technical approaches
- When implementing something that might seem counterintuitive
- When working around a limitation or constraint
- When making architectural decisions that affect future work
- When trade-offs were made (performance vs. readability, etc.)

**When NOT to Create a CIR**:
- Obvious or standard implementations
- Following established patterns already documented
- Temporary workarounds (document those in code comments instead)

## CIR Template

Save CIRs in `docs/change-intent-records/` with descriptive filenames:
- `2026-04-10-library-first-architecture.md`
- `2026-04-15-async-cancellation-tokens.md`

```markdown
# [Descriptive Title]

**Intent:** What was the goal or objective?

**Behaviour:** What are the expected outcomes? (given/when/then)

**Constraints:** What boundaries or guardrails applied?

**Decisions:** What alternatives were considered and rejected, and why?

**Date:** YYYY-MM-DD
```

## Example CIR

```markdown
# Using ISpeedTestService Interface

**Intent:** Allow multiple speed test providers (Ookla, Fast.com, custom)
without changing client code.

**Behaviour:**
- Given: Consumer has ISpeedTestService reference
- When: Provider implementation is swapped via DI
- Then: Consumer code requires no changes

**Constraints:**
- NetPace.Core must remain provider-agnostic
- Public API cannot expose Ookla-specific types
- Must support async/await with cancellation

**Decisions:**
- Chose interface over abstract base class
  - Rejected: Abstract base class (limits inheritance flexibility)
  - Rejected: Direct concrete type (tight coupling to Ookla)
  - Chose: Interface (maximum flexibility, testability, DI-friendly)

**Date:** 2024-08-15
```

## Benefits

1. **Knowledge Transfer**: New team members understand the "why"
2. **AI Context**: LLMs can understand constraints when suggesting changes
3. **Prevent Regressions**: Avoid re-litigating settled decisions
4. **Audit Trail**: Track architectural evolution over time
5. **Code Review**: Reviewers understand trade-offs made

## References

- [Change Intent Records (Bryan Liles)](https://blog.bryanl.dev/posts/change-intent-records/)
- Architecture Decision Records (ADRs) - similar concept

---

**Last Updated**: April 2026
