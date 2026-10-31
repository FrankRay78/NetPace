---
description: Build one GitHub issue to a green, committed feature branch — reads the issue's acceptance criteria, drives RED-GREEN-REFACTOR, and stops. Runs unattended; hand the result to /verify.
argument-hint: "<issue number or URL>"
---

Read `CLAUDE.md` for project context, and `CONVENTIONS.md` as *Repository conventions* below says, before proceeding.

`/build` is the first stage in the `/build` → `/verify` → `/raise-pr` chain: it turns **one GitHub issue** into a green, committed feature branch. It does not format, review, push, open a PR, or merge — `/verify` and `/raise-pr` own all of that, and you run them yourself afterwards.

`/build` is designed to run to completion **without prompting** once it has an issue number, so it can be driven back-to-back as well as invoked directly.

Every step below is written to be repository-agnostic. Wherever a step needs the repository's tools, paths or rules, it says so and names the section of `CONVENTIONS.md` that supplies them (*Repository conventions*), so adopting this command elsewhere means writing that file and touching no step.

**Stop-on-failure is global:** if any step fails — the tree is dirty, the issue is unbuildable as written, the RED tests do not fail, the suite will not go green — STOP at that step, report it, and do not run any later step. Never report READY over a red suite, an unimplemented acceptance criterion, or an uncommitted change.

---

## User Input

```text
$ARGUMENTS
```

A GitHub issue number — bare (`239`), hashed (`#239`), or a full issue URL. **If it is empty, ask for one and wait.** That question is the only interaction `/build` may have with the invoker; everything after it runs unattended.

---

## Repository conventions

The steps in this file are generic. What is particular to the repository they run in — its build, test and format commands, its test conventions, where it states its rules, what it protects, what documentation a change owes — lives in `CONVENTIONS.md` at the root of that repository, written as additions to the steps. Read it before step 1; each step then applies its own rule together with the section of `CONVENTIONS.md` under the matching heading, and where the two conflict, `CONVENTIONS.md` wins. The headings this command reads:

| Heading | Supplies |
|---|---|
| `Build, test and format` | the build-and-test command steps 5 and 6 run, and the formatter step 10 leaves to `/verify` |
| `Test-driven development` | where the project states the TDD rule, and the configuration/tooling carve-out step 5 applies |
| `Testing` | the test conventions step 5 writes to, and the skipped-test ban |
| `Acceptance criteria` | where the project states its traceability chain, and how step 3 checks it |
| `Code quality` | the code constraints step 6 observes |
| `Dependencies` | the dependency-controlled library the *Autonomy contract* protects |
| `Public API and breaking changes` | the protected public API, and any stance that lifts the discussion duty on it |
| `Commit messages` | the commit-message form step 6 uses |
| `Documentation` | the documentation obligations step 8 discharges |
| `Change-intent records` | whether the project keeps them, and the convention step 9 runs |
| `Hooks, scripts and plugins` | the hooks that enforce the bans step 5 names, and what a port must bring |

A section under any other heading is read too, and applied where its text says. Where the file is missing, the command runs on its generic rules alone; where a heading is missing, that step runs on its generic rule alone. Either way the command completes. `CONVENTIONS.md` is read from the root of the repository this command runs in, and trusted as this command file is. The harness itself is not a repository convention: the verdict protocol, the chain scripts that read it and the sibling commands stay named in the steps, so whoever edits the protocol sees what consumes it.

---

## Autonomy contract

Decide every judgement call yourself. Where the issue is ambiguous, pick the reading most consistent with the existing codebase and the issue's own stated intent, **state the assumption in the final report**, and keep going.

Two named exceptions, because the project reserves them for discussion. `CONVENTIONS.md` → *Public API and breaking changes* and *Dependencies* name what each covers:

- **A change to the project's protected public API.** If the issue's acceptance criteria *require* one, the issue is the discussion — proceed, and call it out prominently in the final report so it gets scrutiny at review. If such a change is merely *convenient* and not required by the criteria, do not make it.
- **A new dependency in a library the project marks dependency-controlled.** Do not add one. If the issue cannot be built without it, STOP and report that — the dependency decision is not yours to make unattended.

**Where `CONVENTIONS.md` → *Public API and breaking changes* records a stance on breaking changes that lifts the discussion duty**, that stance governs the first bullet, which is then scope discipline rather than a duty to ask: build what the criteria require, report what breaks and for whom, and do not stop for approval of the break. The dependency bullet is unaffected — a new dependency is not a breaking change.

---

## Steps

1. **Preconditions.** All must hold. Every STOP in this step and the next is a **failure**, so each ends its report with the *Final report*'s failure verdict, carrying the reason given here, as the last line and nothing after it — the explanatory sentence for a human goes *above* it. A stop message whose last line is not the verdict reaches `scripts/chain.sh` as the far less useful `no readable verdict` instead of the reason, and `scripts/chain-next.sh` then parks the issue with that non-reason. These stops are the ones that matter most for that: the chain deliberately does not check the fetch, unpushed commits or the issue itself, because `/build` does.
   - `git status --porcelain` is empty. If not, STOP: "Commit or stash your changes before building." A dirty tree would be swept into the issue's branch at step 4. Reason: `the working tree has uncommitted changes`.
   - `git rev-parse --abbrev-ref HEAD` is `main`. If not, STOP: "Run /build from main — it creates the issue's branch itself." Reason: `/build was not run from main`.
   - `git fetch origin main` succeeds. If not, STOP. Reason: `git fetch origin main failed`.
   - `git log origin/main..main --oneline` is empty. If not, STOP: "Local main has unpushed commits — push or discard them before building." Step 4 branches from `origin/main`, so those commits would be silently absent from the issue's branch. Reason: `local main has unpushed commits`.

2. **Read the issue.** `gh issue view <N>`.
   - If it is closed, or already has an open linked PR, STOP and say which — it is built or in flight. Reason: `the issue is already built or in flight`.
   - If it is open but states no desired behaviour you could build against, STOP and report that you cannot proceed without inventing scope. Reason: `the issue states no behaviour to build against`. This is a report that `/build` cannot do its job — **not** a judgement about whether the issue is large enough to warrant a plan first. That routing decision belongs to whoever drafted the issue and invoked `/build`; take whatever you are handed and build it.
   - Work on this issue only. Do not fold in adjacent improvements you notice along the way — a second mission does not belong in an in-flight branch. Note them in the final report instead.

3. **Read the issue's criteria and its labels — two independent properties.** An issue may carry either, both, or neither; do not treat them as alternatives.

   **What to implement — the acceptance criteria.**
   - If the body has an `## Acceptance criteria` section, that checklist **is** the criteria. Implement every item. This is the shape `/draftissue`'s template mandates, so it is the usual case for a refined issue.
   - Otherwise, derive the criteria from what the body actually states — the observed-vs-expected behaviour of a bug, the described capability of a request — and write them into the final report so the reading can be checked. Do not invent scope to fill a gap.
   - A `## Capability` section's scenarios describe the *flows* the feature must support. Where a checklist is also present the checklist is the fuller list and the one to satisfy; the scenarios are context for the shape of the change, not a substitute for it.

   **How to label the tests — `**Scenario: X**` labels.**
   - If the body carries one or more `**Scenario: X**` labels, give each at least one test carrying a `SCENARIO: X` marker matching the label **exactly** — written in that test file's own comment syntax, so `// SCENARIO: X` where that file's comments open with `//` and `# SCENARIO: X` where they open with `#`. What must match character for character is the name after `SCENARIO:`, not the comment characters before it. This is the project's traceability chain — issue label → test marker — and `CONVENTIONS.md` → *Acceptance criteria* names where the project states it and how to check it. The labels are optional; an issue may carry none, and that is normal, not a defect in the issue. Markers are read from **committed** files, so a marker is only credited once it is committed; run that check to verify.
   - If there are none, add no markers. An invented label is worse than none: it looks like a traceability key and traces to nothing.

4. **Branch.** Off the latest main, named for the issue:

   ```bash
   git checkout -b feature/<N>-<short-slug> origin/main
   ```

   (`/raise-pr` strips the `feature/` prefix and the leading number when it infers the PR title, so `feature/239-input-validation` titles cleanly.) If that branch already exists locally, a prior attempt left it: delete it and recreate, unless it carries commits you have not inspected — in which case STOP and report. Never commit on `main`.

5. **RED — write the failing tests first (the project's TDD rule, NON-NEGOTIABLE).**
   - **First decide whether this change is production code at all.** If the acceptance criteria are satisfied by configuration, tooling, or CI — an `.editorconfig` value, a `.gitattributes` rule, a workflow step — the RED step is the *real tool or gate* failing, not a test in the project's suite. Run it, quote the failure in the final report, and go to step 6. **Never hand-roll a test that reimplements a tool which already performs the check** — it covers strictly less, carries its own bugs, and reports green when its own matching logic fails. `CONVENTIONS.md` → *Test-driven development* names where the project states this carve-out.
   - Otherwise, write the tests for the acceptance criteria from step 3 **before any production code**, following the project's test conventions as `CONVENTIONS.md` → *Testing* gives them. Mock network, filesystem and time.
   - Run the project's build-and-test command (`CONVENTIONS.md` → *Build, test and format*) and **watch the new tests fail** — never against a stale build, and never skipping a test; `CONVENTIONS.md` → *Build, test and format* and *Testing* give both bans, and *Hooks, scripts and plugins* the hooks that enforce them. Quote the actual failure output in the final report; that is the evidence the RED step happened.
   - If the new tests **pass** on first run, the RED step did not happen: either the behaviour already exists (STOP and report that the issue may already be satisfied) or the test does not actually exercise the criterion (fix the test).
   - Do not commit on red. Tests written ahead of the code they call usually do not compile, so they are committed together with the implementation in step 6.

6. **GREEN — minimum change to pass.** On the configuration/tooling path from step 5, make the config or workflow edit, re-run the tool that failed, and quote it passing; add it to CI where it can run there, so the invariant stays gated. Otherwise implement the smallest production-code change that turns those tests green. Either way, then run the project's build-and-test command. The **whole** suite must be green, not just the new tests — a regression elsewhere is a failure. Observe any code constraints `CONVENTIONS.md` → *Code quality* lists. Commit, referencing the issue in imperative mood in the project's commit-message form (`CONVENTIONS.md` → *Commit messages*) (e.g. `Refs #239: validate input before parsing`). Use `Refs #<N>`, never a GitHub closing keyword (`Fix`/`Fixes`/`Close`/`Closes`/`Resolve`/`Resolves` `#<N>`): a closing keyword auto-closes the issue the moment the commit reaches `main`, before the PR is reviewed. The PR body's `Closes #<N>` is the only place that should close it.

7. **REFACTOR — improve on green.** With the tests passing and committed, improve the design if it needs it, and re-run the suite. Still green, or revert the refactor. Never refactor on red.

8. **Discharge the documentation obligations** `CONVENTIONS.md` → *Documentation* lists — every one your change triggers, and no more.

   Re-run the suite if any of this touched code, then commit.

9. **Decide whether a change-intent record is warranted.** Only a project that keeps change-intent records has this decision to make, and `CONVENTIONS.md` → *Change-intent records* says whether this one does. If it keeps none, write none and go to step 10. Otherwise: this is not a documentation obligation and it is not a box to tick — **most changes need none**, and writing one that was not warranted is itself a defect. Run the decision table in the project's change-intent record convention, which that section names: write a record only when a `→ CIR` row applies **and** no `→ No CIR` row does.
   - Weigh the `→ No CIR` rows as carefully as the `→ CIR` ones: they catch the common cases, and that section names the ones that most often apply.
   - If you write one, follow the convention's length target and shape, hold only what is written nowhere else, and link the rest. Do not repeat it in the pull request body — `/raise-pr` points at the record instead.
   - **Name the row that warranted it**, by quoting the first few words of that row's question, both in the commit that adds the record (e.g. `Refs #239: record why input is validated before parsing` with `CIR row: choosing between multiple viable approaches` in the body) and in the final report. If you write no record, say so in the final report.

10. **Stop here.** Do **not** run the project's formatter — that is `/verify`'s formatting pass (step 1a). Do **not** push, open a PR, merge, or run `/verify`. Leave the working tree clean — everything committed to the branch — because `/verify`'s preconditions (step 0) require exactly that.

---

## Final report

- **How you read the issue**: where the acceptance criteria came from (an `## Acceptance criteria` checklist, or derived from the body — and if derived, the criteria themselves), and whether `**Scenario:**` labels were present to mark tests against.
- **RED evidence**: the failing-test output you saw before writing production code.
- **What you changed**, at a behaviour level, and how each acceptance criterion is met.
- **Any assumption** you made on an ambiguous point, any public-API change, and anything you deliberately left out of scope.
- **Change-intent record**: either the record's path and the decision-table row that warranted it, quoted (`CIR row: …`), or a line saying no record was warranted — or, where the project keeps no change-intent records, a line saying so. One of these always appears — silence here reads as a step skipped.
- On the way to a `ready` verdict, add this **above the verdict** — it is boilerplate this command mandates, and appending it below the verdict is the commonest way to lose the verdict: "Run `/verify` to format, gate, review and commit, then `/raise-pr` to push and open the PR — `/raise-pr` derives `Closes #<N>` from this branch name and verifies it before use, then reports what it settled; check that line to confirm the link was made."

**Close the report with the verdict as its last line** — one JSON object on one line, at column 1, nothing else on that line and nothing after it. Exactly one of:

- `{"schema": 1, "outcome": "ready", "branch": "<branch>"}` — every criterion implemented, whole suite green, everything committed, tree clean.
- `{"schema": 1, "outcome": "failed", "reason": "<short reason>"}` — you could not reach that state. Report the wall you actually hit, discovered by working: the criteria conflict, they do not determine the design, the change is larger than they describe. Do not fabricate `ready`.

The schema — its fields, what each outcome carries, and how it is read — is [`docs/verdict-schema.md`](../../docs/verdict-schema.md). `scripts/chain.sh` reads only what ends the report, so one more sentence or a closing pleasantry after the verdict costs the run its verdict and stops the chain; a code fence around the verdict is tolerated, anything after it is not. The upside of reading only the end: quoting either verdict *earlier* in the report — as RED evidence, in a code block, a list or a table — is free and cannot be mistaken for your own.

---
