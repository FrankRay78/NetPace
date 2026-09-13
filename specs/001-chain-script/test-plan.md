# Test Plan — SDLC Command Chain

## Coverage summary

| User Story | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| One issue to a pull request, unattended | ✓ | — | — | — | — | — | 2 |
| A run that goes wrong stops safely and can be diagnosed | — | — | ✓ | ✓ | ✓ | — | 7 |
| Refused before spending anything | — | — | ✓ | — | — | ✓ | 1 |
| Asked what it would do | ✓ | — | ⚠ | — | — | — | 1 |

**Flags:**
- One issue to a pull request, unattended: no Error scenario of its own — deliberate; every failure of the run is specified under "A run that goes wrong stops safely and can be diagnosed".
- Refused before spending anything: one labelled scenario covering five preconditions — each precondition is a separate case under that one scenario (see below), so a single untested precondition shows as a failing case, not a silent gap.
- Asked what it would do: no Error scenario — what report mode does with a missing or malformed issue argument is unspecified; the refusal cases under "Wrong starting point is refused" cover it only for a real run.
- Two scenarios cannot be fully proven by the stubbed test matrix and carry a manual verification step: "One issue to a pull request" (a real model reaching a real pull request) and "A failed stage can be reopened" (a real saved session).

---

### User Story: One issue to a pull request, unattended
The chain is started once against a ready issue and returns an open pull request with no interaction.

#### Scenario: One issue to a pull request
- **WHEN** the chain is started with one ready issue number, from a clean checkout of the default branch that matches the remote, with no terminal input available to it
- **THEN** exactly five stage sessions are started, in this order: build, study, verify, study, raise pull request
- **AND** each study session continues the session of the stage immediately before it, and every other stage starts a fresh session
- **AND** every stage is given the same model
- **AND** the chain ends reporting success and naming the raised pull request's address
- **AND** the chain exits with its success status
- **AND** the working tree is clean afterwards

_Verified by: the stubbed matrix (order, sessions, model, outcome, tree state), plus quickstart §4 for a real model reaching a real pull request._

#### Scenario: Study adds to an existing record
- **WHEN** the issue's study record already holds entries before the later study pass, and that pass ends having added new entries while keeping every existing one
- **THEN** the later study pass is reported as succeeding
- **AND** the next stage (raise pull request) is started
- **AND** the study record afterwards contains every entry it held before the pass, plus the new ones

---

### User Story: A run that goes wrong stops safely and can be diagnosed
A failure at any stage stops the run, names the stage and reason, and leaves everything as that stage left it.

#### Scenario: A failing stage stops the run
- **WHEN** the verify stage (stage 3 of 5) ends reporting its own failure with a reason
- **THEN** no study or raise-pull-request session is started after it
- **AND** the chain's closing message names verify as the failed stage, its position in the run, and the reason verify gave
- **AND** the chain exits with its stage-failure status, distinct from both success and refusal
- **AND** the branch's commits, the checked-out branch and the working tree are identical to how verify left them
- **AND** nothing has been pushed to the remote

#### Scenario: A stage with no readable verdict is a failure
- **WHEN** the build stage ends normally but its report contains neither a success verdict nor a failure verdict
- **THEN** no later stage session is started
- **AND** the chain's closing message names build as the failed stage and says it gave no recognisable verdict
- **AND** the chain exits with its stage-failure status

#### Scenario: A stalled stage ends the run
- **WHEN** a study stage keeps running past the time limit configured for study stages
- **THEN** that stage's process is ended rather than left running
- **AND** no later stage session is started
- **AND** the chain's closing message names that study stage as failed because it stalled
- **AND** the chain exits with its stage-failure status

#### Scenario: A failed stage can be reopened
- **WHEN** a run has stopped on a failing stage, and the invoker then lists recent sessions for the repository
- **THEN** the failed stage's session is the most recent headless session in that list
- **AND** opening it shows that stage's conversation and accepts further input
- **AND** the chain's closing message told the invoker how to reopen it

_Verified by: quickstart §5 (a real saved session). The stubbed matrix covers only the last AND — that the closing message carries reopening guidance._

#### Scenario: A stage that leaves the working tree dirty stops the run
- **WHEN** the build stage reports success but leaves an uncommitted file in the working tree
- **THEN** no later stage session is started
- **AND** the chain's closing message names build as the failed stage because it left the working tree dirty
- **AND** the uncommitted file is still present afterwards
- **AND** the chain exits with its stage-failure status

#### Scenario: A study pass that loses recorded rows stops the run
- **WHEN** the issue's study record holds entries before the later study pass, and that pass reports success but the record afterwards is missing at least one of those entries
- **THEN** no raise-pull-request session is started
- **AND** the chain's closing message names that study pass as failed because the study record lost entries
- **AND** the chain exits with its stage-failure status

#### Scenario: A branch pushed before the final stage stops the run
- **WHEN** every stage up to and including the later study pass succeeds, but the working branch already exists on the remote when the final stage is due
- **THEN** no raise-pull-request session is started
- **AND** the chain's closing message says an earlier stage pushed the branch
- **AND** no pull request exists for the branch
- **AND** the chain exits with its stage-failure status

---

### User Story: Refused before spending anything
A bad starting point is refused before any model session starts.

#### Scenario: Wrong starting point is refused
- **WHEN** the chain is started from each of these starting points in turn, every other precondition being met:
  - no issue argument, or an argument that is not an issue number
  - one of the required tools is not on the path
  - the GitHub tooling is not signed in
  - the working tree has uncommitted changes
  - a branch other than the default branch is checked out
  - the local default branch has commits the remote does not
- **THEN** in every case no stage session of any kind is started
- **AND** the chain's closing message names the precondition that failed and says no stage was started
- **AND** the chain exits with its refusal status, distinct from both success and stage failure
- **AND** the checked-out branch, the working tree and the remote's branches are unchanged
- **AND** the refusal arrives within the spec's bound for an invalid starting point (SC-003)

---

### User Story: Asked what it would do
Report mode lists the stages without running or changing anything.

#### Scenario: Asked what it would do
- **WHEN** the chain is started in report mode with a valid issue number
- **THEN** its output lists the five stages in run order — build, study, verify, study, raise pull request — each with the command it would send for that issue
- **AND** no stage session and no git or GitHub command is started
- **AND** the chain exits with its success status
- **AND** the checked-out branch, the commits, the working tree, the remote's branches and the pull request list are all unchanged

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
