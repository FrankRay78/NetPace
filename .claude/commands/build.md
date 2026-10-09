---
description: Build one GitHub issue to a green, committed feature branch — reads the issue's acceptance criteria, drives RED-GREEN-REFACTOR, and stops. Runs unattended; hand the result to /verify.
---

Read `CLAUDE.md` for project context before proceeding.

`/build` is the first stage in the `/build` → `/verify` → `/raise-pr` chain: it turns **one GitHub issue** into a green, committed feature branch. It does not format, review, push, open a PR, or merge — `/verify` and `/raise-pr` own all of that, and you run them yourself afterwards.

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

**Pre-1.0 override**: while every released version is below 1.0.0, a breaking change to the CLI or to `NetPace.Core` needs no prior discussion or approval and no MAJOR/MINOR/PATCH classification (Constitution VII, *Pre-1.0 stance*). So the first bullet is scope discipline, not a discussion duty: build what the criteria require, report what breaks and for whom, and do not stop for approval of the break. The dependency bullet is unaffected — a new dependency is not a breaking change.

---

## Steps

1. **Preconditions.** All must hold. Every STOP in this step and the next is a **failure**, so each ends its report with the *Final report*'s `FAILED reason=` verdict line as the last line and nothing after it — the explanatory sentence for a human goes *above* it. A stop message whose last line is not the verdict reaches `scripts/chain.sh` as the far less useful `no readable verdict` instead of the reason, and `scripts/chain-next.sh` then parks the issue with that non-reason. These stops are the ones that matter most for that: the chain deliberately does not check the fetch, unpushed commits or the issue itself, because `/build` does.
   - `git status --porcelain` is empty. If not, STOP: "Commit or stash your changes before building." A dirty tree would be swept into the issue's branch at step 4. Verdict: `FAILED reason=the working tree has uncommitted changes`.
   - `git rev-parse --abbrev-ref HEAD` is `main`. If not, STOP: "Run /build from main — it creates the issue's branch itself." Verdict: `FAILED reason=/build was not run from main`.
   - `git fetch origin main` succeeds. If not, STOP with `FAILED reason=git fetch origin main failed`.
   - `git log origin/main..main --oneline` is empty. If not, STOP: "Local main has unpushed commits — push or discard them before building." Step 4 branches from `origin/main`, so those commits would be silently absent from the issue's branch. Verdict: `FAILED reason=local main has unpushed commits`.

2. **Read the issue.** `gh issue view <N>`.
   - If it is closed, or already has an open linked PR, STOP and say which — it is built or in flight. Verdict: `FAILED reason=the issue is already built or in flight`.
   - If it is open but states no desired behaviour you could build against, STOP and report that you cannot proceed without inventing scope. Verdict: `FAILED reason=the issue states no behaviour to build against`. This is a report that `/build` cannot do its job — **not** a judgement about whether the issue is large enough to warrant a plan first. That routing decision belongs to whoever drafted the issue and invoked `/build`; take whatever you are handed and build it.
   - Work on this issue only. Do not fold in adjacent improvements you notice along the way (`CLAUDE.md`: don't fold a second mission into an in-flight branch) — note them in the final report instead.

3. **Read the issue's criteria and its labels — two independent properties.** An issue may carry either, both, or neither; do not treat them as alternatives.

   **What to implement — the acceptance criteria.**
   - If the body has an `## Acceptance criteria` section, that checklist **is** the criteria. Implement every item. This is the shape `/draftissue`'s template mandates, so it is the usual case for a refined issue.
   - Otherwise, derive the criteria from what the body actually states — the observed-vs-expected behaviour of a bug, the described capability of a request — and write them into the final report so the reading can be checked. Do not invent scope to fill a gap.
   - A `## Capability` section's scenarios describe the *flows* the feature must support. Where a checklist is also present the checklist is the fuller list and the one to satisfy; the scenarios are context for the shape of the change, not a substitute for it.

   **How to label the tests — `**Scenario: X**` labels.**
   - If the body carries one or more `**Scenario: X**` labels, give each at least one test carrying a `SCENARIO: X` marker matching the label **exactly** — written in that test file's own comment syntax, so `// SCENARIO: X` in a C# test and `# SCENARIO: X` in a `*.tests.sh` matrix. What must match character for character is the name after `SCENARIO:`, not the comment characters before it. This is the Constitution §VIII traceability chain — issue label → test marker — and §VIII is the rule this step enforces. The labels are optional; an issue may carry none, and that is normal, not a defect in the issue. Markers are read from **committed** files, so a marker is only credited once it is committed; verify with `bash scripts/traceability-check.sh`.
   - If there are none, add no markers. An invented label is worse than none: it looks like a traceability key and traces to nothing.

4. **Branch.** Off the latest main, named for the issue:

   ```bash
   git checkout -b feature/<N>-<short-slug> origin/main
   ```

   (`/raise-pr` strips the `feature/` prefix and the leading number when it infers the PR title, so `feature/239-server-screening` titles cleanly.) If that branch already exists locally, a prior attempt left it: delete it and recreate, unless it carries commits you have not inspected — in which case STOP and report. Never commit on `main`.

5. **RED — write the failing tests first (Constitution §I, NON-NEGOTIABLE).**
   - **First decide whether this change is production code at all.** If the acceptance criteria are satisfied by configuration, tooling, or CI — an `.editorconfig` value, a `.gitattributes` rule, a workflow step — the RED step is the *real tool or gate* failing, not an xUnit test. Run it, quote the failure in the final report, and go to step 6. **Never hand-roll a test that reimplements a tool which already performs the check** — it covers strictly less, carries its own bugs, and reports green when its own matching logic fails (Constitution §I, *Configuration, tooling and CI changes*).
   - Otherwise, write the tests for the acceptance criteria from step 3 **before any production code**. Follow `docs/conventions/csharp-style.md` and `CLAUDE.md`'s testing section: xUnit, Given-When-Then, `MethodName_Scenario_ExpectedResult`, mirroring the source file's name. Mock network, filesystem and time.
   - Run `dotnet build ./src && dotnet test ./src` and **watch the new tests fail.** Never `--no-build` — the `green-gate.sh` hook denies it when stale, and rightly. Quote the actual failure output in the final report; that is the evidence the RED step happened.
   - If the new tests **pass** on first run, the RED step did not happen: either the behaviour already exists (STOP and report that the issue may already be satisfied) or the test does not actually exercise the criterion (fix the test).
   - Do not commit on red. Tests written ahead of the code they call usually do not compile, so they are committed together with the implementation in step 6. Do not use `Skip`, `[Fact(Skip=…)]`, `Assert.Skip` or any of the family; the `no-skipped-tests.sh` commit hook blocks them, and Constitution §X bans them.

6. **GREEN — minimum change to pass.** On the configuration/tooling path from step 5, make the config or workflow edit, re-run the tool that failed, and quote it passing; add it to CI where it can run there, so the invariant stays gated. Otherwise implement the smallest production-code change that turns those tests green. Either way, then run `dotnet build ./src && dotnet test ./src`. The **whole** suite must be green, not just the new tests — a regression elsewhere is a failure. Keep production code trim/AOT-safe (no reflection-heavy APIs). Commit, referencing the issue in imperative mood per the constitution's git workflow (e.g. `Refs #239: screen candidates before measuring`). Use `Refs #<N>`, never a GitHub closing keyword (`Fix`/`Fixes`/`Close`/`Closes`/`Resolve`/`Resolves` `#<N>`): a closing keyword auto-closes the issue the moment the commit reaches `main`, before the PR is reviewed. The PR body's `Closes #<N>` is the only place that should close it.

7. **REFACTOR — improve on green.** With the tests passing and committed, improve the design if it needs it, and re-run the suite. Still green, or revert the refactor. Never refactor on red.

8. **Discharge the documentation obligations** that apply to what you changed (`CLAUDE.md`'s paired rules):
   - Any new or changed **public API in `NetPace.Core`** needs `///` XML docs — they ship to NuGet consumers.
   - Any changed **CLI option** needs the README.md `--help` snapshot and USER_GUIDE.md updated.
   - Any **release-pipeline** change needs `docs/RELEASING.md` updated.
   - Consider a **Change-Intent Record** per `docs/conventions/change-intent-records.md` if the change is non-obvious.
   - Write markdown one line per paragraph — no hard column wrapping.

   Re-run the suite if any of this touched code, then commit.

9. **Stop here.** Do **not** run `dotnet format` — that is `/verify`'s formatting pass (step 1a). Do **not** push, open a PR, merge, or run `/verify`. Leave the working tree clean — everything committed to the branch — because `/verify`'s preconditions (step 0) require exactly that.

---

## Final report

- **How you read the issue**: where the acceptance criteria came from (an `## Acceptance criteria` checklist, or derived from the body — and if derived, the criteria themselves), and whether `**Scenario:**` labels were present to mark tests against.
- **RED evidence**: the failing-test output you saw before writing production code.
- **What you changed**, at a behaviour level, and how each acceptance criterion is met.
- **Any assumption** you made on an ambiguous point, any public-API change, and anything you deliberately left out of scope.
- On the way to a `READY` verdict, add this **above the verdict line** — it is boilerplate this command mandates, and appending it below the verdict is the commonest way to lose the verdict: "Run `/verify` to format, gate, review and commit, then `/raise-pr` to push and open the PR — `/raise-pr` derives `Closes #<N>` from this branch name and verifies it before use, then reports what it settled; check that line to confirm the link was made."

**Close the report with the verdict on its own last line** — plain, at column 1, nothing else on that line and nothing after it. Exactly one of:

- `READY branch=<branch>` — every criterion implemented, whole suite green, everything committed, tree clean.
- `FAILED reason=<short reason>` — you could not reach that state. Report the wall you actually hit, discovered by working: the criteria conflict, they do not determine the design, the change is larger than they describe. Do not fabricate READY.

`scripts/chain.sh` reads the last non-blank line of the report and nothing else, so one more sentence, a closing pleasantry or a code fence after the verdict costs the run its verdict and stops the chain. The upside of reading only that line: quoting either verdict *earlier* in the report — as RED evidence, in a code block, a list or a table — is free and cannot be mistaken for your own. Decoration on that one line is tolerated, not invited: one leading heading, bullet, numbered-item or blockquote marker, a wrapping run of `*`, `_` or backticks, and an indent of up to three spaces are stripped. Nothing else is — a four-space indent is a code block, and a line carrying anything besides the verdict is read as no verdict at all.
