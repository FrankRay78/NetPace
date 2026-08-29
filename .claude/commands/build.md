---
description: Build one GitHub issue to a green, committed feature branch — reads the issue's acceptance criteria, drives RED-GREEN-REFACTOR, and stops. Runs unattended; hand the result to /ship.
---

Read `CLAUDE.md` for project context before proceeding.

`/build` is the stage before `/ship`: it turns **one GitHub issue** into a green, committed feature branch. It does not format, review, push, open a PR, or merge — `/ship` and `/raise-pr` own all of that, and you run them yourself afterwards.

`/build` is designed to run to completion **without prompting** once it has an issue number, so it can be driven back-to-back as well as invoked directly.

**Stop-on-failure is global:** if any step fails — the tree is dirty, the issue is unbuildable as written, the RED tests do not fail, the suite will not go green — STOP at that step, report it, and do not run any later step. Never report READY over a red suite, an unimplemented acceptance criterion, or an uncommitted change.

---

## User Input

```text
$ARGUMENTS
```

A GitHub issue number — bare (`239`), hashed (`#239`), or a full issue URL. **If it is empty, ask for one and wait.** That question is the only interaction `/build` may have with the invoker; everything after it runs unattended.

---

## Autonomy contract

Decide every judgement call yourself. Where the issue is ambiguous, pick the reading most consistent with the existing codebase and the issue's own stated intent, **state the assumption in the final report**, and keep going.

Two named exceptions, because `CLAUDE.md` requires discussion for them:

- **Public `NetPace.Core` API changes.** If the issue's acceptance criteria *require* one, the issue is the discussion — proceed, and call it out prominently in the final report so it gets scrutiny at review. If a public API change is merely *convenient* and not required by the criteria, do not make it.
- **New `NetPace.Core` dependencies.** Do not add one. If the issue cannot be built without it, STOP and report that — the dependency decision is not yours to make unattended.

---

## Steps

1. **Preconditions.** All must hold:
   - `git status --porcelain` is empty. If not, STOP: "Commit or stash your changes before building." A dirty tree would be swept into the issue's branch at step 4.
   - `git rev-parse --abbrev-ref HEAD` is `main`. If not, STOP: "Run /build from main — it creates the issue's branch itself."
   - `git fetch origin main` succeeds.

2. **Read the issue.** `gh issue view <N>`.
   - If it is closed, or already has an open linked PR, STOP and say which — it is built or in flight.
   - If it is open but states no desired behaviour you could build against, STOP and report that you cannot proceed without inventing scope. This is a report that `/build` cannot do its job — **not** a judgement about whether the issue is large enough to warrant a plan first. That routing decision belongs to whoever drafted the issue and invoked `/build`; take whatever you are handed and build it.
   - Work on this issue only. Do not fold in adjacent improvements you notice along the way (`CLAUDE.md`: don't fold a second mission into an in-flight branch) — note them in the final report instead.

3. **Classify the issue — this decides how the tests are labelled.** Scan the body for `**Scenario: X**` labels:

   - **Traceable issue** (one or more `**Scenario: X**` labels — the shape `/speckit.draftissue` produces). These *are* the acceptance criteria. Implement every one of them, and give each at least one test carrying a `// SCENARIO: X` marker whose text matches the label **exactly**. This preserves the Constitution §VIII chain — issue label → test marker — with the spec/test-plan hop collapsed out, since the issue is already the spec.
   - **Free-form issue** (no `**Scenario:**` labels — a bug report, a plain feature request). Derive the acceptance criteria from what the body actually states: the observed-vs-expected behaviour of a bug, or the described capability of a request. Write them out in the final report so the reviewer can check your reading. **Do not add `// SCENARIO:` markers** in this mode — an invented label is worse than none, because it looks like a traceability key and traces to nothing.

   Either way, **do not create a `specs/` folder.** `/build` deliberately sits outside the spec-kit pipeline: the traceability Stop hook reads active specs only, so with none present it is a clean no-op, and `/raise-pr`'s spec-cleanup step correctly finds nothing to delete. If an issue genuinely warrants a full spec, run the spec-kit chain instead of `/build`.

4. **Branch.** Off the latest main, named for the issue:

   ```bash
   git checkout -b feature/<N>-<short-slug> origin/main
   ```

   (`/raise-pr` strips the `feature/` prefix and the leading number when it infers the PR title, so `feature/239-server-screening` titles cleanly.) If that branch already exists locally, a prior attempt left it: delete it and recreate, unless it carries commits you have not inspected — in which case STOP and report. Never commit on `main`.

5. **RED — write the failing tests first (Constitution §I, NON-NEGOTIABLE).**
   - Write the tests for the acceptance criteria from step 3 **before any production code**. Follow `docs/conventions/csharp-style.md` and `CLAUDE.md`'s testing section: xUnit, Given-When-Then, `MethodName_Scenario_ExpectedResult`, mirroring the source file's name. Mock network, filesystem and time.
   - Run `dotnet build ./src && dotnet test ./src` and **watch the new tests fail.** Never `--no-build` — the `green-gate.sh` hook denies it when stale, and rightly. Quote the actual failure output in the final report; that is the evidence the RED step happened.
   - If the new tests **pass** on first run, the RED step did not happen: either the behaviour already exists (STOP and report that the issue may already be satisfied) or the test does not actually exercise the criterion (fix the test).
   - Commit the red phase — `test: red phase for #<N> — <short description>`. Do not use `Skip`, `[Fact(Skip=…)]`, `Assert.Skip` or any of the family; the `no-skipped-tests.sh` commit hook blocks them, and Constitution §X bans them.

6. **GREEN — minimum production code to pass.** Implement the smallest change that turns those tests green, then run `dotnet build ./src && dotnet test ./src`. The **whole** suite must be green, not just the new tests — a regression elsewhere is a failure. Keep production code trim/AOT-safe (no reflection-heavy APIs). Commit, referencing the issue in imperative mood per the constitution's git workflow (e.g. `Fix #239: screen candidates before measuring`).

7. **REFACTOR — improve on green.** With the tests passing and committed, improve the design if it needs it, and re-run the suite. Still green, or revert the refactor. Never refactor on red.

8. **Discharge the documentation obligations** that apply to what you changed (`CLAUDE.md`'s paired rules):
   - Any new or changed **public API in `NetPace.Core`** needs `///` XML docs — they ship to NuGet consumers.
   - Any changed **CLI option** needs the README.md `--help` snapshot and USER_GUIDE.md updated.
   - Any **release-pipeline** change needs `docs/RELEASING.md` updated.
   - Consider a **Change-Intent Record** per `docs/conventions/change-intent-records.md` if the change is non-obvious.
   - Write markdown one line per paragraph — no hard column wrapping.

   Re-run the suite if any of this touched code, then commit.

9. **Stop here.** Do **not** run `dotnet format` (that is `/ship` step 1a). Do **not** push, open a PR, merge, or run `/ship`. Leave the working tree clean — everything committed to the branch — because `/ship` requires exactly that at its step 0.

---

## Final report

- **How you read the issue**: traceable or free-form, and — for a free-form issue — the acceptance criteria you derived from it.
- **RED evidence**: the failing-test output you saw before writing production code.
- **What you changed**, at a behaviour level, and how each acceptance criterion is met.
- **Any assumption** you made on an ambiguous point, any public-API change, and anything you deliberately left out of scope.
- Then exactly one of:
  - `READY branch=<branch>` — every criterion implemented, whole suite green, everything committed, tree clean. Follow with: "Run `/ship` to format, gate, review and raise the PR — and add `Closes #<N>` to the PR body if you want the issue to close on merge."
  - `FAILED reason=<short reason>` — you could not reach that state. Report the wall you actually hit, discovered by working: the criteria conflict, they do not determine the design, the change is larger than they describe. Do not fabricate READY.
