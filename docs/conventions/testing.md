# Testing Guide

> This document elaborates the *Acceptance criteria* rules in `CONVENTIONS.md` and must remain consistent with them.

**Scope**: How to author a test so it verifies the acceptance criterion rather than the implementation that happens to satisfy it
**Extends**: `CONVENTIONS.md` → *Testing* (read that first for the essential testing patterns — file naming, what to test in `NetPace.Core`, console snapshots)
**Audience**: Read before writing tests, and when reviewing tests someone else wrote

---

## Tests verify outcome, not mechanism

A test's assertions must describe an outcome an outside observer can verify, not the specific mechanism the implementation chose to deliver it. This is the test-layer expression of the outcome-not-mechanism rule in [`CONVENTIONS.md` → *Acceptance criteria*](../../CONVENTIONS.md#acceptance-criteria).

**The independence test**: would this test still pass under a different reasonable implementation of the same acceptance criterion? If no, the test is pinning mechanism, not outcome — rewrite it before relying on it.

| Avoid (mechanism, brittle) | Prefer (outcome, durable) |
|---|---|
| Assert `GetFastestServerByLatencyAsync` was called once on a mock | Assert the reported server is the one with the lowest measured latency |
| Assert each latency request is timed by a particular timer API | Assert the reported latency is the average of the measured round trips, over the number of requests `LatencyTestSettings.LatencyTestIterations` sets — a public setting, so it is named exactly |
| Assert the JSON is written by `System.Text.Json` with a given naming policy | Assert `DownloadSpeedBitsPerSecond` in the JSON output equals the measured bits per second — an interface contract, so it is named exactly |
| Assert `--no-latency --no-download --no-upload` produces an exact error string | Assert the command exits non-zero and the composed output (snapshot) tells the user no tests are left to run |
| Assert stdout contains the literal string `"Mbps"` | Assert the output presents the measured throughput in a unit the caller can interpret |
| Assert the result row is rendered via `Spectre.Console.Table` with column widths 12/8/8 | Assert the results are presented as a tabular summary readable in a terminal |

**Do not assert against**: CSS classes, DOM IDs or element types, animation specifics, font names/weights/colours, and pixel measurements; HTTP methods, endpoint paths, and status codes; response/payload schemas (JSON keys, field names); database tables, collections, columns, or indexes; algorithm or protocol choices (hash functions, signature schemes, encryption modes); framework or library picks; storage technology; specific URLs or ports; timing values (Ns / Nms) and polling cadences; exact error message strings; and log line formats or log levels. These are implementation choices — a test that pins one converts a free choice into a contract.

That list is the mechanism enumeration from `CONVENTIONS.md` → *Acceptance criteria*, written for *acceptance criteria*; read at the test layer it is a guide to the independence test above, not a literal ban. `CONVENTIONS.md` → *Testing* separately permits a targeted assertion on a single observable fact — an exit code, or one specific line present or absent — and NetPace's suite relies on that. The question to ask is always whether the assertion pins something the acceptance criterion promised, or something the implementation merely happened to choose. An interface contract the criterion names (§IX) — a JSON or CSV field, an option name, an exit code — is something it promised, so the test asserts it exactly and that is not mechanism.

Note the interaction with NetPace's console-snapshot rule (`CONVENTIONS.md` → *Testing*): a snapshot of composed console output is not a mechanism assertion. The snapshot *is* the user-visible contract, which is precisely the observable outcome. What this section rules out is hand-picking an internal rendering detail — a Spectre widget type, a column width — and asserting on that instead.

A test that ends in a snapshot does not also assert on text the snapshot already shows, present or absent. `Assert.Contains`, `Assert.DoesNotContain` or `TryGetProperty` checks ahead of a `Verify` restate part of the snapshot, so they catch nothing the snapshot would miss and leave two places to update when the output changes. Keep the facts a snapshot cannot hold, such as the exit code or the equality of two runs, and let the snapshot carry everything about what the output says.

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

### Several sites proven by one mutation

A test added to pin existing behaviour has no natural RED phase, so the evidence is a mutation: break the code, watch the test fail, restore it. Breaking several sites at once and seeing one failure proves only that one of them is pinned — the rest can still be reverted with the suite green. Mutate each site on its own and run the full suite each time, and survey every sibling site before writing a test, not only the ones a review named: the sweep before shows which sites survive, and the sweep after shows each is caught by the test written for it.

### Wall-clock lower bound with no timer slack

A test that measures how long something took belongs on a controlled clock: `OoklaSpeedtest` takes its elapsed time from the time provider it is given. Where the real pause is itself the behaviour under test — a double that waits before each report — a lower bound of exactly *pauses × delay* fails intermittently: `Task.Delay` fires on the runtime's coarse timer, so a pause can end a fraction of a millisecond short by the `Stopwatch`, and `ElapsedMilliseconds` truncates that to a whole millisecond less. Allow a millisecond per pause, and keep the floor high enough that a version making fewer pauses still fails.

---

## Scenario traceability

Where the GitHub issue being built carries a `**Scenario: X**` label, at least one test must carry a `SCENARIO: X` marker naming it exactly — `// SCENARIO: X` in a C# test, `# SCENARIO: X` in a `*.tests.sh` matrix. `CONVENTIONS.md` → *Acceptance criteria* is the rule; the short version is that the name matches character-for-character, any mix of `/` and `#` comment characters may precede it, the label is optional, and a marker that names no label is worse than no marker at all because it looks like a traceability key and traces nothing. Whitespace at the two ends is trimmed on both sides before comparing, so only the *internal* spacing of the name is load-bearing.

The label must be a line of its own in the issue body, outside any code fence. A `**Scenario: X**` written mid-sentence, in backticks, or inside a fenced block is deliberately not read — those describe the convention rather than declaring a scenario — so the worked examples in §VIII and `/draftissue` are shown fenced for presentation and must not be copied into an issue with their fences.

Run `bash scripts/traceability-check.sh` before raising a PR: it reads the issue number off the branch and names any label whose marker is missing. The `traceability` CI job runs the same script on every PR and is a required check, so a dropped marker blocks the merge; the local run catches it before pushing. It reads **committed** test files only — a marker that is merely written, or written and staged, does not count — and it ignores docs, including this one, which restates the rule and must never be what satisfies it.
