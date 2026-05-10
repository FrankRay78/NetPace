# speckit.testplan

Generate a test plan for the current feature from its specification and technical plan.

---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

---

## Purpose

This command translates completed, clarified requirements into named, verifiable test
scenarios. It is **not** requirements gathering (that is `/speckit.specify` and
`/speckit.clarify`) and it is **not** test code generation.

Its sole output is `specs/$ARGUMENTS/test-plan.md` — a human-readable contract
between the specification and the implementation, reviewed and approved before any test
code is written.

The traceability chain this command participates in is:

```
spec.md `**Scenario: [name]**`
   ↓  (this command)
test-plan.md `#### Scenario: [name]`
   ↓  /speckit.implement
test code `// SCENARIO: [name]` comment
```

Every `**Scenario: [name]**` label in spec.md MUST produce exactly one
`#### Scenario: [name]` heading in test-plan.md, with names matching
character-for-character (case and punctuation included).

---

## Pre-Generation Quality Check

Before generating any scenarios, read `specs/$ARGUMENTS/spec.md` and assess
the quality of the **Acceptance Scenarios** under each `### User Story N` section.
Each `**Scenario: [name]**` label in spec.md becomes one test-plan scenario.

Flag and report any **scenario** that exhibits one or more of these problems:

| Problem | Signal | Action |
|---|---|---|
| Missing label | A `Given/When/Then` block with no `**Scenario: [name]**` above it | Flag — bare scenarios break the spec ↔ test-plan ↔ test-code traceability chain; the spec must add a label before test planning continues |
| Vague criterion | "fast", "responsive", "handles errors", "works correctly" | Flag — cannot produce a measurable scenario |
| Internal state focus | THEN describes what the system stores/computes rather than what a caller observes | Flag — scenario will be unverifiable from outside |
| Compound scenario | A single `**Scenario:**` label contains "and" across multiple independent behaviours | Flag — should be split into multiple labelled scenarios |
| Duplicate label | Two `**Scenario: [name]**` labels share the same name (case-insensitive) within spec.md | Flag — names must be unique to act as a traceability key |

Also flag at the **User Story** level:

| Problem | Signal | Action |
|---|---|---|
| Missing failure modes | A User Story has only happy-path scenarios | Flag — almost always means failure modes were not specified |
| Single scenario | A User Story has only one `**Scenario:**` label total | Flag — a single scenario cannot cover a user journey |

If **any** flags are raised, output them clearly:

```
⚠ Pre-generation issues found in spec.md:

- User Story 1: scenario "Login responds quickly" — "responds quickly" is not measurable.
  Quantify before test planning (e.g. "Login responds within 200ms under normal load").
- User Story 2: only happy-path scenarios. What happens if the export exceeds size
  limits, or the destination is unavailable?
- User Story 3: bare Given/When/Then block (no `**Scenario:**` label) at line N —
  add a `**Scenario: [Descriptive name]**` label before continuing (every acceptance
  scenario must carry a label).

Proceed anyway? These issues will produce thin or untestable scenarios.
```

If **Missing label** was raised, **stop** — bare Given/When/Then blocks cannot be
turned into `#### Scenario: [name]` headings without a name, so the spec must add
the label first. Do not offer "Proceed anyway?" for this case.

For all other flagged issues, wait for confirmation before proceeding.

---

## Scenario Class Coverage

For each User Story, you MUST attempt to produce scenarios in every applicable class.
A complete test plan covers all of the following where they apply:

- **Primary (happy path)** — the system behaves correctly under normal, valid conditions
- **Alternate flow** — valid input that takes a different but legitimate path
- **Error / Exception** — invalid input, violated preconditions, or dependency failures
- **Boundary** — values at or just beyond the edges of valid ranges
- **Recovery** — the system's behaviour after a failure, and whether state is consistent
- **Non-functional** — observable performance, security, or accessibility properties
  (only include where the spec contains measurable non-functional requirements)

If a User Story has **no Error/Exception scenarios**, flag it in the summary — this
almost always means failure modes were not specified, not that they don't exist.

If a User Story has **only one scenario total**, flag it — a single scenario cannot
cover a user journey's full surface.

---

## What Makes a Good Scenario

**The test for a good scenario: can a developer write a failing test directly from
the WHEN and THEN lines, without reading any other document?**

THEN must describe an **observable output** — something a caller can assert against
from outside the system. It must never describe internal state.

### ✅ CORRECT — observable output

```
#### Scenario: Login rejected for unknown email
- **WHEN** a POST is made to /auth/login with an email address not present in the system
- **THEN** the response status is 401
- **AND** the response body contains error code AUTH_INVALID_CREDENTIALS
- **AND** no authentication token is included in the response
```

### ❌ WRONG — internal state, not observable

```
#### Scenario: Login rejected for unknown email
- **WHEN** a user logs in with an unknown email
- **THEN** the user is not authenticated
- **AND** the database is not updated
```

The wrong version requires knowledge of the database and the concept of "authenticated"
as internal state. A test cannot assert against either without implementation knowledge.

### ✅ CORRECT — boundary scenario

```
#### Scenario: Rate limit triggers on the fifth consecutive failure
- **WHEN** exactly 5 consecutive failed login attempts are made for the same email
  within a 10-minute window
- **THEN** the fifth attempt returns status 401 (normal rejection)
- **AND** the sixth attempt within the same window returns status 429
- **AND** the Retry-After response header is present on the 429 response
```

### ❌ WRONG — vague, unverifiable

```
#### Scenario: Too many login attempts are blocked
- **WHEN** a user tries to log in too many times
- **THEN** the account is locked
```

"Too many times" is not a number. "Account is locked" is internal state. Neither is
testable without reading implementation code.

---

## Format

Output to `specs/$ARGUMENTS/test-plan.md`.

The file must follow this structure exactly:

```markdown
# Test Plan — [feature name]

## Coverage summary
[Generated after all scenarios are written — see Post-Generation section]

---

### User Story: [Brief Title — copied from spec.md `### User Story N - [Brief Title]` heading, without the priority suffix]
[Optional one-line description from the User Story body if helpful for reviewers]

#### Scenario: [name — copied character-for-character from spec.md `**Scenario: [name]**` label]
- **WHEN** [specific action, input, or precondition — expand the spec.md "When" clause with concrete detail]
- **THEN** [observable outcome — response code, return value, visible change, error]
- **AND** [additional observable assertion, if needed]
```

### Naming rules

- `### User Story:` headers in test-plan.md correspond to `### User Story N - [Brief Title]` headings in spec.md. Use the brief title only — drop the `User Story N - ` prefix and the `(Priority: PN)` suffix.
- `#### Scenario:` names in test-plan.md MUST match the `**Scenario: [name]**` labels in spec.md **exactly** — character for character, including case and punctuation. This is the traceability key linking spec.md → test-plan.md → test code.
- Scenario names must be descriptive enough to become a test method name without modification: "Login rejected for unknown email" not "Unknown email test".
- One scenario = one independently executable test. No compound scenarios.
- Each `**Scenario:**` label in spec.md MUST produce exactly one `#### Scenario:` heading in test-plan.md — no merging, no splitting.

### WHEN rules

- Expand the spec.md "When" clause with concrete detail (specific endpoint, method, value, condition).
- State preconditions explicitly if they differ from the default ("given an account that has already been locked").

### THEN rules

- Expand the spec.md "Then" clause with concrete observables.
- Every THEN must be assertable by calling code — a status code, a return value, a visible UI change, the presence or absence of an element, an error type.
- Every AND adds a further assertion on the same response/state.
- Never describe what the system "does not do" unless paired with what it does instead:
  ❌ "the user is not redirected" — untestable
  ✅ "the response status is 401 and the current page remains /login" — testable

---

## Content triage

- Soft cap: if scenarios under a single User Story exceed **25**, the User Story is
  almost certainly too broad. Flag it and ask whether it should be split before continuing.
- The spec.md `**Scenario:**` labels are the source of truth — do not invent new
  scenarios in test-plan.md, and do not silently drop any. If a scenario looks
  duplicate or wrong, raise it as a pre-generation flag and ask the spec author to fix
  spec.md first.
- If 4 or more low-risk boundary cases test the same axis (e.g. string length limits)
  and the spec.md author has labelled each separately, that is the author's choice —
  preserve them. Suggest consolidation as a comment in the pre-generation flag, do not
  silently merge.

---

## Post-Generation: Coverage Summary

After all scenarios are written, prepend a coverage summary table to the file:

```markdown
## Coverage summary

| User Story | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| Record a third-party interaction against an identity | ✓ | — | ✓ | — | — | — | 11 |
| Retrieve the chronological history for an identity | ✓ | — | ✓ | — | — | — | 4 |
| ...           |   |   |   |   |   |   |   |

**Flags:**
- Retrieve the chronological history for an identity: no Recovery scenario — is system
  state consistent if the read path is unavailable?
- Demonstrate cross-organisation replication: only 2 scenarios — likely missing
  failure-mode coverage.
```

Use `✓` where the class is covered, `—` where it is absent (and absence is acceptable
given the User Story), and `⚠` where the class is absent and its absence looks
like a gap.

---

## Rules

- Do NOT write any test code
- Do NOT generate tasks
- Do NOT invent scenarios not labelled in spec.md
- Do NOT silently drop a scenario labelled in spec.md
- Do NOT begin if spec.md does not exist at the expected path
- Do NOT proceed if any spec.md `**Scenario:**` label is malformed or missing — every acceptance scenario MUST carry a `**Scenario: [name]**` label
- WHEN lines describe inputs and conditions — never implementation behaviour
- THEN lines describe observable outputs — never internal state or side effects
- Each scenario must be independently executable (no scenario depends on another having run first)
- test-plan.md lives alongside spec.md and plan.md in `specs/$ARGUMENTS/`
  — it is a specification artifact, not a test artifact

---

## Implementation Guidance Footer

After writing all scenarios and the coverage summary, append the following section
verbatim to the end of `test-plan.md`:

```markdown
---

## Implementation guidance

Every test method that implements a scenario in this plan MUST include a `// SCENARIO:`
comment whose value matches the `#### Scenario:` name above **exactly** — character for
character, including case, punctuation, and internal whitespace. Leading and trailing
whitespace on the scenario name is trimmed before comparison.

```csharp
[Fact]
public void Login_UnknownEmail_Returns401()
{
    // SCENARIO: Login rejected for unknown email

    // ...
}
```

`/speckit.testchecklist` validates these comments against the scenario names in this
file. A test without a matching `// SCENARIO:` comment is reported as untraced.
```
