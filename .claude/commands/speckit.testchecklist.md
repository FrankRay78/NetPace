# speckit.testchecklist

Static analysis: verify that the implementation has fully honoured the test plan.
Reads source files only — does not run tests, execute code, or parse test runner output.

---

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

---

## Purpose

This command answers one question: **has every scenario in test-plan.md been
implemented as a real, honest test?**

It is run after implementation is complete and before a PR is opened. It produces
a structured report. It does not modify any files. It does not run the test suite.
Branch protection enforces 100% green — this command enforces coverage and integrity.

---

## Operating Constraints

**STRICTLY READ-ONLY.** Do not modify any file under any circumstance.

**NO TEST EXECUTION.** Do not run `dotnet test`, `pytest`, `npm test`, or any
equivalent. Do not invoke any shell command that executes code. All analysis is
performed by reading source files only.

**NO INFERENCE FROM TEST RUNNER OUTPUT.** Do not read `TestResults/`, `coverage/`,
`playwright-report/`, or any file produced by a test run. Conclusions must be
derivable from static source analysis alone.

---

## Pre-Execution Checks

1. **Verify test-plan.md exists**:
   Path: `.specify/specs/$ARGUMENTS/test-plan.md`
   If missing: stop — the test plan is the source of truth. Cannot check without it.

2. **Locate the test directory**:
   Determine the test file location from `.specify/specs/$ARGUMENTS/plan.md`.
   Common patterns: `tests/`, `src/__tests__/`, `NetPace.Tests/`, `MyProject.Tests/`.
   If ambiguous, search for files matching the test framework naming convention
   (`*Tests.cs`, `*.test.ts`, `test_*.py`) from the repo root.
   If no test files are found: report this immediately and stop.

3. **Parse test-plan.md — extract the scenario inventory**:
   Collect every `#### Scenario:` header from test-plan.md.
   Record: requirement name (parent `### Requirement:` header) and scenario name.
   This is the **expected set**. Report the count before proceeding:
   ```
   Test plan: 4 requirements, 18 scenarios
   ```

---

## Analysis Steps

### Step 1 — Build the scenario inventory from test-plan.md

For each `### Requirement:` block, collect all `#### Scenario:` names beneath it.
Store as a flat list of (requirement, scenario) pairs. This is the **expected set**.

Normalise scenario names for matching: lowercase, collapse whitespace, strip
punctuation. Store both the normalised and original forms.

### Step 2 — Build the test inventory from source files

Read every test file in the test directory. For each file:

1. **Extract `// SCENARIO:` comments** — these are the explicit traceability markers.
   Capture the full text after `// SCENARIO:` on each line. Normalise as above.

2. **Extract test method names** — collect every test method/function name regardless
   of whether it has a `// SCENARIO:` comment. Normalise to readable form
   (convert `Login_Rejected_For_Unknown_Email` to "login rejected for unknown email").

3. **Record per test**:
   - File path and line number
   - Test method name (normalised)
   - `// SCENARIO:` comment text (normalised), or MISSING if absent
   - Integrity flags (see Step 3)

### Step 3 — Flag integrity problems in each test

For every test method found, check for the following. Each is a distinct finding:

#### 3a. Missing SCENARIO comment
The test has no `// SCENARIO:` comment. It cannot be traced back to test-plan.md.

```
⚠ MISSING SCENARIO COMMENT
  File: NetPace.Tests/AuthTests.cs:47
  Test: Login_With_Empty_Body
  Cannot verify this test corresponds to any test-plan.md scenario.
```

#### 3b. SCENARIO comment does not match any scenario in test-plan.md

The `// SCENARIO:` text (after normalisation) does not match any scenario name in
test-plan.md. Likely a renamed scenario, typo, or undocumented addition.

```
⚠ UNMATCHED SCENARIO REFERENCE
  File: NetPace.Tests/AuthTests.cs:89
  Comment: // SCENARIO: Login with malformed JSON body
  This scenario does not appear in test-plan.md.
  Closest match: "Login rejected for malformed request" (similarity: high)
```

#### 3c. Trivially passing assertion

The test body contains a pattern that will always pass regardless of the
implementation under test. Check for:

- `Assert.True(true)` / `assertTrue(True)` / `expect(true).toBe(true)`
- `Assert.Pass()` or equivalent
- Test body that is empty or contains only comments
- Test body containing only variable declarations with no assertion

```
CRITICAL — TRIVIALLY PASSING ASSERTION
  File: NetPace.Tests/RateLimitTests.cs:134
  Test: Rate_Limit_Resets_After_Window_Expires
  Body contains Assert.True(true) — this test cannot fail.
```

#### 3d. Skipped or suppressed test

The test is marked to be excluded from execution:

- `[Fact(Skip = "...")]` / `[Theory(Skip = "...")]` (xUnit)
- `[Ignore]` / `[Ignore("...")]` (NUnit / MSTest)
- `@pytest.mark.skip` / `@pytest.mark.xfail`
- `test.skip(...)` / `xit(...)` / `xtest(...)` (Jest/Vitest)
- `pending()` (Jasmine)

```
CRITICAL — SKIPPED TEST
  File: NetPace.Tests/TokenTests.cs:201
  Test: Expired_Refresh_Token_Is_Rejected
  Marked: [Fact(Skip = "not implemented yet")]
  This scenario is not being verified.
```

#### 3e. Mock configured to satisfy the assertion

Detect where a mock return value is configured to return exactly what the assertion
expects, with the mock object being asserted against directly rather than a real
implementation. Look for the pattern:
- Mock set up with `Returns(...)` / `ReturnsAsync(...)` / `setup(...).returns(...)`
- Returned value matches the asserted value
- The asserted object is the mock itself, not a real class

```
HIGH — POSSIBLE MOCK SELF-SATISFACTION
  File: NetPace.Tests/AuthTests.cs:67
  Test: Login_Rejected_For_Unknown_Email
  Mock IAuthService configured to return StatusCode=401.
  Test asserts StatusCode==401 against the mock object directly.
  Verify the SUT is the real implementation, not the mock.
  Cannot be confirmed statically — requires human review.
```

#### 3f. Stub or NotImplemented placeholder not replaced

The test calls a method whose entire body is `throw new NotImplementedException()`
or calls a class that was generated as a red-phase stub.

```
HIGH — STUB NOT REPLACED
  File: NetPace.Tests/AuthTests.cs:45
  Test: Successful_Login_With_Valid_Credentials
  Calls AuthService.LoginAsync() — implementation body throws NotImplementedException.
  Red-phase stub has not been replaced with real implementation.
```

---

### Step 4 — Match test-plan.md scenarios to tests

For each scenario in the expected set, attempt to find a corresponding test using
this matching order:

1. **Exact match on `// SCENARIO:` comment** (normalised) — highest confidence
2. **Fuzzy match on test method name** (normalised) — medium confidence, flag for review
3. **No match found** — scenario is uncovered

Build a coverage table:

| Requirement | Scenario | Coverage | Confidence | File | Line |
|---|---|---|---|---|---|
| User Login | Successful login with valid credentials | ✓ | Exact | AuthTests.cs | 23 |
| User Login | Login rejected for unknown email | ✓ | Exact | AuthTests.cs | 47 |
| User Login | Login rejected for wrong password | ✓⚠ | Fuzzy | AuthTests.cs | 71 |
| Rate Limiting | Account locked after threshold | ✗ MISSING | — | — | — |
| Rate Limiting | Rate limit resets after window expires | ✗ INTEGRITY | — | RateLimitTests.cs | 134 |

Coverage codes:
- `✓` — matched, no integrity issues
- `✓⚠` — matched, but has a non-critical integrity issue (e.g. missing comment)
- `✗ MISSING` — no test found for this scenario
- `✗ INTEGRITY` — test found but has a critical integrity issue (skip, trivial pass)

---

### Step 5 — Detect undocumented tests

For every test that could not be matched back to any test-plan.md scenario
(no matching `// SCENARIO:` comment, no fuzzy name match), report it:

```
⚠ UNDOCUMENTED TEST
  File: NetPace.Tests/AuthTests.cs:198
  Test: Login_Should_Log_Audit_Event
  No corresponding scenario in test-plan.md.
  Either: (a) add this scenario to test-plan.md if it represents a real requirement,
  or (b) remove the test if it was added speculatively during implementation.
```

---

## Output Report

Produce a single structured Markdown report to stdout. Do not write any files.

```
# Test Checklist Report — $ARGUMENTS

Generated by static analysis. No tests were executed.

## Summary

| Metric | Value |
|---|---|
| Scenarios in test-plan.md | 18 |
| Scenarios with matching test | 15 |
| Scenarios missing a test | 2 |
| Scenarios with integrity issues | 1 |
| Undocumented tests | 3 |
| Critical issues | 2 |
| High issues | 1 |
| Warnings | 4 |

Overall: NOT READY — 2 critical issues must be resolved before PR

---

## Coverage Table

[full table as described in Step 4]

---

## Integrity Issues

### CRITICAL

1. [finding from Step 3]

### HIGH

2. [finding from Step 3]

### WARNINGS

3. [finding from Step 3]

---

## Undocumented Tests

[list from Step 5]

---

## Next Actions

NOT READY — resolve before opening PR:
- [specific action for each critical/high finding]

  or

READY WITH WARNINGS — human review recommended:
- [specific action for each warning]

  or

READY — all scenarios covered, no integrity issues found.
```

---

## Severity Reference

| Severity | Definition | Blocks PR |
|---|---|---|
| CRITICAL | Test is skipped, trivially passing, or scenario has no test at all | Yes |
| HIGH | Possible mock self-satisfaction, stub not replaced | Requires human review |
| WARNING | Missing `// SCENARIO:` comment, fuzzy match only, undocumented test | No |

---

## Rules

- NEVER run tests or execute code of any kind
- NEVER read test runner output files
- NEVER modify any file
- NEVER infer pass/fail status from anything other than static source analysis
- If a scenario cannot be located with confidence, mark it MISSING — do not assume
  it is covered
- Fuzzy matches are warnings not confirmations — the human reviewer decides
- Report zero issues gracefully with a READY verdict and full coverage table
