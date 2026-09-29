# Testing Guide

> This document elaborates Section IX (Behavioural Specification) of the constitution and must remain consistent with it.

**Scope**: How to author a test so it verifies the acceptance criterion rather than the implementation that happens to satisfy it
**Extends**: `CLAUDE.md` (read that first for the essential testing patterns — file naming, what to test in `NetPace.Core`, console snapshots)
**Audience**: Read before writing tests, and when reviewing tests someone else wrote

---

## Tests verify outcome, not mechanism

A test's assertions must describe an outcome an outside observer can verify, not the specific mechanism the implementation chose to deliver it. This is the test-layer expression of Principle IX (Behavioural Specification) in [the constitution](../constitution.md).

**The independence test**: would this test still pass under a different reasonable implementation of the same acceptance criterion? If no, the test is pinning mechanism, not outcome — rewrite it before relying on it.

| Avoid (mechanism, brittle) | Prefer (outcome, durable) |
|---|---|
| Assert the response body contains the literal string `"Invalid email address"` | Assert the caller is told the input was rejected, and given an error code identifying the validation failure |
| Assert the response body is `{"status":"ok","data":[...]}` with HTTP 200 | Assert the caller can retrieve the current list of services in a single successful response |
| Assert the setting is written to the `user_preferences` table and cached in Redis | Assert the setting is remembered across sessions and visible on the next sign-in |
| Assert the endpoint returns `[]` when no records match | Assert the caller is shown a clear empty-result state when no records match |
| Assert stdout contains the literal string `"Mbps"` | Assert the output presents the measured throughput in a unit the caller can interpret |
| Assert the result row is rendered via `Spectre.Console.Table` with column widths 12/8/8 | Assert the results are presented as a tabular summary readable in a terminal |

**Do not assert against**: CSS classes, DOM IDs or element types, animation specifics, font names/weights/colours, and pixel measurements; HTTP methods, endpoint paths, and status codes; response/payload schemas (JSON keys, field names); database tables, collections, columns, or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values (Ns / Nms) and polling cadences; exact error message strings; and log line formats or log levels. These are implementation choices — a test that pins one converts a free choice into a contract.

Note the interaction with NetPace's console-snapshot rule (`CLAUDE.md` → Testing): a snapshot of composed console output is not a mechanism assertion. The snapshot *is* the user-visible contract, which is precisely the observable outcome. What this section rules out is hand-picking an internal rendering detail — a Spectre widget type, a column width — and asserting on that instead.

**Regression exception**: a test that pins a specific mechanism is permitted only when it exists to prevent a named, previously-fixed bug. Reference the bug in the test name or a one-line comment at the site (e.g. `// GH#176 — release archive omitted .pdb after AOT publish`), so a future reader knows why the coupling is deliberate.

---

## What makes a good scenario

**The test for a good scenario: can a developer write a failing test directly from it, without reading any other document?**

The expected outcome must be an **observable output** — something a caller can assert against from outside the system. It must never be internal state.

### ✅ Correct — observable output

> **Scenario: Login rejected for unknown email**
> Given an email address not present in the system, when a login is attempted with it, then the caller is told the credentials were invalid, is given an error code identifying the failure, and receives no authentication token.

### ❌ Wrong — internal state, not observable

> **Scenario: Login rejected for unknown email**
> Given an unknown email, when a user logs in, then the user is not authenticated and the database is not updated.

The wrong version needs knowledge of the database, and of "authenticated" as an internal flag. A test cannot assert against either without implementation knowledge.

### ✅ Correct — boundary scenario with concrete numbers

> **Scenario: Rate limit triggers after the fifth consecutive failure**
> Given exactly 5 consecutive failed login attempts for the same email within a 10-minute window, when a sixth attempt is made in that window, then it is refused as rate-limited rather than as a normal credential rejection, and the caller is told when it may retry.

### ❌ Wrong — vague and unverifiable

> **Scenario: Too many login attempts are blocked**
> When a user tries to log in too many times, then the account is locked.

"Too many times" is not a number, and "the account is locked" is internal state. Neither can be turned into an assertion.

**In short:** name concrete boundary numbers rather than "too many" or "large"; state the outcome a caller can see rather than the state the system holds; and write the expected outcome specifically enough that two people reading it would write the same assertion.

---

## Test integrity

A test can pass while verifying nothing. These are the failure modes to check for — in your own tests before committing, and in a test you are reviewing. Each one reports green, so none of them shows up as a gap in a coverage report.

### Trivially passing assertion

The body contains a pattern that passes regardless of the implementation under test:

- `Assert.True(true)`, `Assert.Pass()` or equivalent
- A body that is empty, or contains only comments
- A body containing only variable declarations, with no assertion at all

Such a test cannot fail, so it never had a RED phase.

### Mock configured to satisfy its own assertion

A mock is set up to return exactly what the assertion then expects, and the asserted object is the mock itself rather than a real implementation. The pattern to look for is a `Returns(...)` / `ReturnsAsync(...)` setup whose configured value is the same value the assertion checks for.

This verifies that the mocking library works. Mock the *collaborator* (network, filesystem, clock) and assert against the real class under test — never against the mock.

### Red-phase stub never replaced

The test calls a method whose body is still `throw new NotImplementedException()`, or a class generated as a scaffold during the RED phase. The test may well be failing honestly, but if it passes, the implementation it claims to cover does not exist.

---

## Scenario traceability

Where the GitHub issue being built carries a `**Scenario: X**` label, at least one test must carry a `// SCENARIO: X` marker naming it exactly. Constitution §VIII is the rule; the short version is that names match character-for-character, the label is optional, and a marker that names no label is worse than no marker at all because it looks like a traceability key and traces nothing.
