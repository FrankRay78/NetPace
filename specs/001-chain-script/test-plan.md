# Test Plan — SDLC Command Chain

## Coverage summary

| User Story | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| One issue to a pull request, unattended | ✓ | — | — | — | — | — | 1 |
| A run that goes wrong stops safely and can be diagnosed | — | — | ✓ | ✓ | ✓ | — | 4 |
| Refused before spending anything | — | — | ✓ | — | — | — | 1 |
| Asked what it would do | ✓ | — | — | — | — | — | 1 |

**Flags:**
- One issue to a pull request, unattended: a single scenario and no Error scenario of its own — deliberate; every failure of the run is specified under "A run that goes wrong stops safely and can be diagnosed".
- Refused before spending anything: one scenario covering three refusals, each a separate case under it.
- By hand, not in the automated matrix (author's rule — anything needing elaborate stubs is checked manually): "A failed stage can be reopened", and the real-model half of "One issue to a pull request". See quickstart §4 and §5.
- Automated cases live in `scripts/chain.tests.sh` and carry `# // SCENARIO: <name>` markers. `/speckit.testchecklist` must be pointed at that file; it does not discover `.sh` tests on its own.

---

### User Story: One issue to a pull request, unattended
The chain is started once against a ready issue and returns an open pull request with no interaction.

#### Scenario: One issue to a pull request
- **WHEN** the chain is started with one issue number, from a clean checkout of the default branch, with no terminal input available, and every stage reports success
- **THEN** exactly five stage sessions are started, in this order: build, study, verify, study, raise pull request
- **AND** the first study session continues the build session, and the second continues the verify session
- **AND** the chain ends reporting success with the raised pull request's address
- **AND** the chain exits successfully

_Automated with a stub for order, sessions and outcome; a real model reaching a real pull request is quickstart §4._

---

### User Story: A run that goes wrong stops safely and can be diagnosed
A failure at any stage stops the run, names the stage and reason, and leaves everything as that stage left it.

#### Scenario: A failing stage stops the run
- **WHEN** the verify stage (stage 3 of 5) ends reporting its own failure with a reason
- **THEN** no stage session is started after it
- **AND** the chain's closing message names verify, its position in the run, and the reason verify gave
- **AND** the closing message says how to reopen the failed session
- **AND** the chain exits unsuccessfully
- **AND** the checked-out branch, its commits and the working tree are as verify left them

#### Scenario: A stage with no readable verdict is a failure
- **WHEN** the build stage ends normally but its report contains no success verdict
- **THEN** no later stage session is started
- **AND** the chain's closing message names build as failed for giving no recognisable verdict
- **AND** the chain exits unsuccessfully

#### Scenario: A stalled stage ends the run
- **WHEN** a stage keeps running past the configured stage time limit
- **THEN** that stage's process is ended rather than left running
- **AND** no later stage session is started
- **AND** the chain's closing message names that stage as failed because it stalled
- **AND** the chain exits unsuccessfully

#### Scenario: A failed stage can be reopened
- **WHEN** a real run has stopped on a failing stage, and the invoker then lists recent sessions for the repository
- **THEN** the failed stage's session is the most recent headless session in that list
- **AND** opening it shows that stage's conversation and accepts further input

_By hand: quickstart §5._

---

### User Story: Refused before spending anything
A bad starting point is refused before any stage starts.

#### Scenario: Wrong starting point is refused
- **WHEN** the chain is started from each of these starting points in turn, the others being valid:
  - no issue argument
  - uncommitted changes in the working tree
  - a branch other than the default checked out
- **THEN** in every case no stage session is started
- **AND** the chain's message says what was wrong
- **AND** the chain exits unsuccessfully
- **AND** the checked-out branch and working tree are unchanged

---

### User Story: Asked what it would do
Report mode lists the stages without running or changing anything.

#### Scenario: Asked what it would do
- **WHEN** the chain is started in report mode with a valid issue number
- **THEN** its output lists the five stages in run order — build, study, verify, study, raise pull request — each with the command it would send for that issue
- **AND** no stage session is started
- **AND** the chain exits successfully
- **AND** the checked-out branch, commits and working tree are unchanged

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
