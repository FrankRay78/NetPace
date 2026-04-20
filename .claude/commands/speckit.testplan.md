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

---

## Pre-Generation Quality Check

Before generating any scenarios, read `specs/$ARGUMENTS/spec.md` and assess
the quality of each `### Requirement:` block.

Flag and report any requirement that exhibits one or more of these problems:

| Problem | Signal | Action |
|---|---|---|
| Vague criterion | "fast", "responsive", "handles errors", "works correctly" | Flag — cannot produce a measurable scenario |
| Missing failure modes | No mention of invalid input, unavailable dependencies, or boundary violations | Flag — will produce happy-path-only scenarios |
| Internal state focus | Describes what the system stores/computes rather than what a caller observes | Flag — scenarios will be unverifiable from outside |
| Compound requirement | A single `### Requirement:` block contains "and" across multiple independent behaviours | Flag — should be split before test planning |

If **any** flags are raised, output them clearly:

```
⚠ Pre-generation issues found in spec.md:

- "User Login": criterion "responds quickly" is not measurable. Quantify before
  test planning (e.g. "responds within 200ms under normal load").
- "Data Export": no failure modes specified. What happens if the export exceeds
  size limits, or the destination is unavailable?

Proceed anyway? These issues will produce thin or untestable scenarios.
```

Wait for confirmation before proceeding if issues are found.

---

## Scenario Class Coverage

For each requirement, you MUST attempt to produce scenarios in every applicable class.
A complete test plan covers all of the following where they apply:

- **Primary (happy path)** — the system behaves correctly under normal, valid conditions
- **Alternate flow** — valid input that takes a different but legitimate path
- **Error / Exception** — invalid input, violated preconditions, or dependency failures
- **Boundary** — values at or just beyond the edges of valid ranges
- **Recovery** — the system's behaviour after a failure, and whether state is consistent
- **Non-functional** — observable performance, security, or accessibility properties
  (only include where the spec contains measurable non-functional requirements)

If a requirement has **no Error/Exception scenarios**, flag it in the summary — this
almost always means failure modes were not specified, not that they don't exist.

If a requirement has **only one scenario total**, flag it — a single scenario cannot
cover a requirement's full surface.

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

### Requirement: [name — copied exactly from spec.md]
[Requirement text — copied exactly from spec.md]

#### Scenario: [unique, descriptive name]
- **WHEN** [specific action, input, or precondition]
- **THEN** [observable outcome — response code, return value, visible change, error]
- **AND** [additional observable assertion, if needed]
```

### Naming rules

- `### Requirement:` headers must match `spec.md` exactly — this is the traceability key
- `#### Scenario:` names must be unique across the entire file
- Scenario names must be descriptive enough to become a test method name without
  modification: "Login rejected for unknown email" not "Unknown email test"
- One scenario = one independently executable test. No compound scenarios.

### WHEN rules

- Be specific about the exact input, state, or action
- Include the specific endpoint, method, value, or condition
- State preconditions explicitly if they differ from the default ("given an account
  that has already been locked")

### THEN rules

- Every THEN must be assertable by calling code — a status code, a return value,
  a visible UI change, the presence or absence of an element, an error type
- Every AND adds a further assertion on the same response/state
- Never describe what the system "does not do" unless paired with what it does instead:
  ❌ "the user is not redirected" — untestable
  ✅ "the response status is 401 and the current page remains /login" — testable

---

## Content triage

- Soft cap: if raw scenario candidates exceed **25 for a single requirement**, the
  requirement is almost certainly too broad. Flag it and ask whether it should be
  split before continuing.
- Merge near-duplicate scenarios that differ only in incidental detail (two scenarios
  testing "wrong password" and "incorrect password" are the same scenario).
- If 4 or more low-risk boundary cases test the same axis (e.g. string length limits),
  consolidate into one parametric scenario that names the values explicitly:
  "Rejects usernames shorter than 3 characters or longer than 50 characters"

---

## Post-Generation: Coverage Summary

After all scenarios are written, prepend a coverage summary table to the file:

```markdown
## Coverage summary

| Requirement | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| User Login | ✓ | — | ✓ | — | — | — | 3 |
| Rate Limiting | ✓ | — | ✓ | ✓ | — | — | 4 |
| ...           |   |   |   |   |   |   |   |

**Flags:**
- Rate Limiting: no Recovery scenario — is system state consistent if the rate
  limit store is unavailable?
- Password Reset: only 1 scenario for the "Token Validation" requirement — likely
  missing expiry and replay attack cases.
```

Use `✓` where the class is covered, `—` where it is absent (and absence is acceptable
given the requirement), and `⚠` where the class is absent and its absence looks
like a gap.

---

## Rules

- Do NOT write any test code
- Do NOT generate tasks
- Do NOT invent requirements not present in spec.md
- Do NOT begin if spec.md does not exist at the expected path
- WHEN lines describe inputs and conditions — never implementation behaviour
- THEN lines describe observable outputs — never internal state or side effects
- Each scenario must be independently executable (no scenario depends on another
  having run first)
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
character, including case and punctuation:

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
