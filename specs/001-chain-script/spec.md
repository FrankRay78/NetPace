# Feature Specification: SDLC Command Chain

**Feature Branch**: `feature/270-chain-script`

**Created**: 2026-09-13

**Status**: Draft

**Input**: User description: "#270 — Add scripts/chain.sh — run the SDLC command chain end to end for one issue"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One issue to a pull request, unattended (Priority: P1)

Someone has an issue ready to build. They start the chain once, naming that issue, and walk away. The chain runs the project's stages in a fixed order — build, study, verify, study, raise the pull request — starting each only after the previous one has reported success, and they come back to an open pull request. Nothing asks them anything along the way.

**Why this priority**: This is the whole point of the feature. Today the invoker types each command, waits, reads its report and decides whether to start the next; the waiting, not the work, turns an hour into a day.

**Independent Test**: Run the chain against a small, ready issue from a clean checkout of the default branch and confirm an open pull request results with no prompt at any point.

**Acceptance Scenarios**:

**Scenario: One issue to a pull request**
Given a ready issue and a clean checkout of the default branch, When the chain runs, Then the issue is built, its surprises recorded, verified and reviewed, and raised as a pull request — with no prompting at any point.

**Scenario: Study adds to an existing record**
Given a study pass has already recorded against an issue earlier in the run, When the later study pass records against the same issue, Then it adds to that record rather than replacing it.

---

### User Story 2 - A run that goes wrong stops safely and can be diagnosed (Priority: P1)

Something fails partway through. The chain stops at once, runs nothing further, tells the invoker which stage failed and why, and leaves the branch exactly as that stage left it. The invoker can reopen the failed stage's session, point an assistant at the branch to troubleshoot, and finish the remaining stages by hand without redoing the stages that already succeeded.

**Why this priority**: Equal to Story 1. A chain that pushes on over unverified work is worse than no chain — the stages are expensive, and the final one publishes a pull request.

**Independent Test**: Force a stage to fail (for example, leave a precondition of a later stage unmet) and confirm no later stage runs, the failed stage is named with its reason, the branch is unchanged by the chain, and the failed session can be reopened.

**Acceptance Scenarios**:

**Scenario: A failing stage stops the run**
Given a chain run in which one stage does not reach success, When that stage ends, Then no later stage runs, the invoker is told which stage failed and why, and the branch is left exactly as the failing stage left it.

**Scenario: A stage with no readable verdict is a failure**
Given a stage that ends without reporting a success the chain can recognise, When the chain evaluates that stage, Then it treats the stage as failed and stops the run.

**Scenario: A stalled stage ends the run**
Given a stage that stops making progress, When it exceeds the time allowed for that stage, Then the stage is ended, reported as a failed stage, and no later stage runs.

**Scenario: A failed stage can be reopened**
Given a run that stopped on a failing stage, When the invoker goes to diagnose it, Then they can reopen that stage's session and continue working in it.

**Scenario: A stage that leaves the working tree dirty stops the run**
Given a stage that reports success but leaves uncommitted changes in the working tree, When that stage ends, Then no later stage runs and the invoker is told which stage left the tree dirty.

**Scenario: A study pass that loses recorded rows stops the run**
Given an issue whose study record already holds entries before the later study pass, When that pass ends with any of those entries gone, Then no later stage runs and the invoker is told the study record lost entries.

**Scenario: A branch pushed before the final stage stops the run**
Given a run in which the branch already exists on the remote before the final stage starts, When the chain reaches the final stage, Then the final stage does not run and the invoker is told an earlier stage pushed.

---

### User Story 3 - Refused before spending anything (Priority: P2)

Someone starts the chain from the wrong place — a feature branch, a dirty working tree, a default branch with unpushed commits, or without naming an issue. The chain refuses straight away, says which precondition failed, and has spent no model time.

**Why this priority**: Cheap protection against the most common mistakes. Without it the first expensive stage would discover the problem after a model session has already started.

**Independent Test**: Start the chain from a feature branch and from a dirty tree; confirm each stops immediately with the failed precondition named and no model session started.

**Acceptance Scenarios**:

**Scenario: Wrong starting point is refused**
Given a starting point that fails any precondition — no issue named, a required tool missing or not signed in, uncommitted changes, a branch other than the default checked out, or local default-branch commits the remote lacks — When the chain is started, Then it stops before starting any stage, names the precondition that failed, and changes nothing.

---

### User Story 4 - Asked what it would do (Priority: P3)

Before trusting the chain with a real issue, someone asks it to report rather than run. It lists the stages it would run, in order, for the given issue, and does nothing else.

**Why this priority**: Makes the chain safe to exercise against the real repository, which is how it is verified. Useful but not required to get an issue to a pull request.

**Independent Test**: Ask the chain what it would do for any issue; confirm the stage list is printed and the repository, remote and pull requests are unchanged.

**Acceptance Scenarios**:

**Scenario: Asked what it would do**
Given any issue, When the chain is asked to report rather than run, Then it names the stages it would run in order and makes no change of any kind — no branch, no commit, no push, no pull request.

---

### Edge Cases

- **A study pass finds nothing to record.** That is success, not failure; the working tree is left untouched and the chain continues.
- **A study pass reports failure** (including "could not gather any evidence"). The chain stops — no stage, study included, may fail silently.
- **A stage's verdict is not the last thing it prints.** The chain still recognises it; the verdict's position in the report must not decide success.
- **A stage reports success but leaves the working tree dirty.** The next stage's clean-tree precondition would fail; the chain stops and names the stage that left the tree dirty rather than letting the next stage fail confusingly.
- **The final stage cannot confirm the issue link** (for example, the issue-lookup check could not run). The pull request is still raised, so the stage has succeeded; the chain reports what the stage said about the link.
- **The final stage ends without producing a pull request.** Failure, however the stage phrases its stop.
- **A permission the stages would normally ask for is silently refused in an unattended run.** A stage may carry on degraded and still report success. This is a documented residual risk, not something the chain detects.
- **The CLI or GitHub tooling is missing or not signed in.** Detected before any stage starts where it can be checked without spending model time; otherwise the first stage fails and the chain surfaces the tool's own error.
- **The issue number names a pull request, a closed issue, or nothing.** The first stage refuses it; the chain stops on that stage's failure.

## Requirements *(mandatory)*

### Functional Requirements

**Ordering and gating**

- **FR-001**: The chain MUST take exactly one issue per invocation and run these stages in this fixed order: build, study, verify, study, raise pull request.
- **FR-002**: The chain MUST start a stage only after the previous stage has reported success in a form the chain reads directly from that stage's own output.
- **FR-003**: The chain MUST treat any stage that reports failure, reports nothing recognisable as success, or ends abnormally as failed.
- **FR-004**: The chain MUST recognise each stage's success by that stage's own existing verdict, not by a marker the chain asks the stage to emit. For the final stage, success is the presence of the raised pull request's address in its output.
- **FR-005**: The chain MUST start each stage as a new, separate process. A study pass continues the exact session of the stage it follows; every other stage starts a fresh session. The chain MUST NOT prompt the invoker at any point during a run.

**Preconditions**

- **FR-006**: Before starting any stage, the chain MUST verify that an issue was named, the working tree is clean, the default branch is checked out, and the local default branch carries no commits that the remote does not have. If any check fails it MUST stop, name the failed check, and change nothing.

**Failure behaviour**

- **FR-007**: On the first failed stage the chain MUST stop, run no later stage, and tell the invoker which stage failed and why.
- **FR-008**: The chain MUST leave the branch and working tree exactly as the failing stage left them. It MUST NOT reset, clean, stash, or delete anything in the working tree, and MUST NOT delete or force-update any branch — on failure or otherwise.
- **FR-009**: After a run stops, the failing stage's session MUST be reopenable for diagnosis.
- **FR-010**: Each stage MUST be given a time limit appropriate to that stage; a stage exceeding it MUST be ended and reported as failed.
- **FR-011**: After a stage that should leave a clean working tree reports success, the chain MUST confirm the tree is clean before starting the next stage, and treat a dirty tree as a failure of the stage that left it.

**Study passes**

- **FR-012**: Each study pass MUST run against the work of the stage it follows, with that stage's conversation available to it.
- **FR-013**: After each study pass the working tree MUST be clean; a study pass that records nothing MUST leave the tree untouched.
- **FR-014**: The second study pass MUST add to the issue's existing study record rather than replace it. If the record lost entries it had before the second pass, the chain MUST tell the invoker and stop as a failure of that pass.

**Outward-facing actions**

- **FR-015**: The chain MUST perform exactly one outward-facing action — the pull request raised by its final stage. No earlier stage may push, open, or modify a pull request.

**Report mode**

- **FR-016**: When asked to report rather than run, the chain MUST list the stages it would run, in order, for the given issue, and MUST make no change of any kind: no branch, commit, push, pull request, or model session.

**Configuration and documentation**

- **FR-017**: Every stage in a run MUST use the same model, chosen in one place in the chain and passed to each stage.
- **FR-018**: The chain MUST keep no logs or session records of its own beyond what each stage's session already retains. Which session each stage ran in is known to the chain only for the duration of the run and is not shown to the invoker.
- **FR-019**: The chain MUST be documented alongside the project's agentic workflow guidance, covering prerequisites, how to invoke it (including report mode), what to do when a stage fails, and the residual risk of silently refused permissions in unattended runs.

### Key Entities

- **Chain run**: One invocation against one issue. Has an ordered list of stages, a current stage, and an outcome (completed with a pull request, stopped at a named stage, or refused at a precondition).
- **Stage**: One of the project's existing commands run as its own session. Has a position in the order, a time limit, and a verdict read from its own output (success or failure, with a reason).
- **Study record**: The issue's accumulating list of recorded surprises, added to by each study pass and never replaced.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A full run from a ready issue to an open pull request takes one invocation and zero invoker interactions.
- **SC-002**: In every run where a stage fails, zero later stages start, and the invoker can name the failed stage and its reason from the chain's closing message alone.
- **SC-003**: A run started from an invalid starting point stops in under 10 seconds with no model session started.
- **SC-004**: A report-mode run leaves the local repository, remote branches and pull request list identical to their state before the run.
- **SC-005**: Across a full run, the pull request is the only change visible outside the invoker's machine.
- **SC-006**: Someone who has not used the chain before can run it and recover from a failed stage using only its documentation.

## Assumptions

- **One chain at a time.** The invoker never runs chains concurrently. The chain still keeps track of which session each stage ran in, so a study pass reaches exactly the right conversation even while the invoker has another session open in the same repository. After a failure, the failed stage's session is the most recent headless one, so the invoker reopens it through the normal means of resuming a recent session.
- **Nothing is carried from verify to the final stage.** Verify fixes its in-scope review findings itself; the final stage runs fresh against the branch. Review findings verify defers as out of scope are named only in verify's own report, which the chain shows as that stage completes.
- **The stages are used as they are.** Build, study, verify and raise-pull-request are not changed by this feature; the chain adapts to their existing preconditions, verdicts and reports.
- **Branch naming belongs to the build stage.** The chain does not dictate branch names.
- **Runs in the invoker's own checkout**, from the project's usual shell environment. A shell-only script is sufficient; no separate Windows-native version ships.
- **Stage time limits start conservative** and are tuned from real runs; stages differ by an order of magnitude, so they are set per stage rather than as one value.
- **The model starts as a current-generation model** and is changed in one place when a newer one is preferred.
- **Verification is by report mode and manual runs**, not an automated test suite — this is operator tooling, and report mode is what makes exercising it against the real repository safe.
- **Automating the final, outward-facing stage is accepted knowingly.** The deliberate human act moves from raising the pull request to starting the chain against one named issue.
- **Out of scope**: choosing which issue to build, retrying, parking, merging, resuming from a chosen stage, building more than one issue per invocation, and running anywhere but the invoker's own checkout.
