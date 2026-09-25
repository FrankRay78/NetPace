<!--
Provenance: this is the GENERIC, stack-portable workflow guide. Keep it stack-neutral: NetPace-specific behaviour belongs in the delta doc alongside it, agentic-workflow-NetPace.md.
-->

# Agentic Software Development Workflow

## Introduction

Write a spec before touching code, lock a test plan before writing a test, and **enforce both mechanically**. The result is a *harness*: an agent (Claude Code, Codex CLI, …) constrained by context, feedback loops and automated quality gates, so the agent does the work and the engineer reviews it.

**The workflow in one line:** draft, review and confirm an issue → build it by one of two routes (the full spec route for a large feature: spec → test plan → tasks → implement; or the lighter build route for an issue that is already its own spec) → verify → raise the PR → merge.

This document is **stack-generic**. A project implements it by adding the files in the [Appendix](#appendix--codebase-setup) and recording its deviations in a short companion "implementation delta" doc, so this guide stays the single shared source of truth across repos.

*Inspired by:*
- [Coding with AI](https://www.chrismdp.com/coding-with-ai/) — Chris Parsons, 2026.
- [Harness Engineering](https://openai.com/index/harness-engineering/) — OpenAI, 2026.
- [Effective harnesses for long-running agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) — Anthropic, 2025.
- Field notes — Andrej Karpathy (Dec 2025) and Boris Cherny (Feb 2026).

### Which route do I use?

Every piece of work starts as a drafted, reviewed and confirmed issue. The route depends on whether that issue can serve as the spec:

- **Build route** — the issue already states checkable acceptance criteria: a bug with observed and expected behaviour, a small feature, a docs or tooling change. `/build` works straight from the issue; there is no other planning document. Every stage from `/build` to `/raise-pr` runs unattended, so the whole route can run from one command (see *Running the stages end to end*).
- **Full spec route** — the work is too large or uncertain for an issue: several user flows, open design questions, or a test plan that must be agreed before any code exists. The spec, test plan and task list add review checkpoints the build route lacks.

When in doubt, try the build route. If `/build` reports that the issue does not settle the design, the issue needs a spec.

---

## Why a harness, not better prompts

When an agent struggles with trivial work, treat it as a **context/harness failure, not a prompt failure** (chrismdp). The model is rarely the bottleneck: GitHub Copilot running a frontier model underperforms a purpose-built CLI harness, because the wrapper (context selection, the agent loop, the gates) matters as much as the model.

A useful frame is the **five duties of a harness** (OpenAI): **constrain** what the agent may do, **inform** it of what it should do, **verify** its work, **correct** its mistakes, and **keep humans in the loop at high-stakes decisions.** Every section below maps to one of these; the one teams most often under-build is *verify*.

**The bottleneck has moved from generation to verification** (chrismdp): not "how fast can we build?" but "how fast can we tell if it's right?" Two consequences run through this workflow:
- If verifying an AI change takes as long as writing it, present it differently, move the verification to an automated gate, or don't delegate it.
- **Give the agent a way to verify its own work** — a test loop, a type checker, a browser check. Output quality rises sharply with one (Boris Cherny: ~2–3×).

---

## Workflow Execution Order

> Slash-command names are the reference Claude Code/spec-kit set; a project may rename them. The *sequence* is the contract, not the names.

Every feature goes through the shared issue stage, then one of two routes (see *Which route do I use?*); both routes finish the same way.

### Shared — the issue stage (on the main branch)
1. `/speckit.draftissue` ← optional; turn an unstructured brief into a well-formed issue
2. `/speckit.reviewissue` ← pre-spec gate; posts gaps + recommendations as an issue comment
3. `/speckit.confirmissue` ← fold the answered review into a `## Confirmed decisions` section and label the issue `ready`

### Route A — the full spec route (large features)
4. `/speckit.specify`
5. `/speckit.clarify` ← iterate until the spec feels complete
6. `/speckit.checklist` ← resolve all gaps before continuing
7. `/speckit.plan`
8. `/speckit.testplan` ← review output carefully before continuing
9. *red-phase commit* ← commit `test-plan.md` as locked intent (script per project)
10. `/speckit.tasks`
11. `/speckit.analyze` ← resolve HIGH/CRITICAL before branching; runs the test-plan cross-check via the `after_analyze` hook
12. `/speckit.implement` ← runs to suite-green, keeping the suite green on the inner loop at its own discretion. A **soft standard, not a per-turn gate**: the binding "green before a PR" guarantee is `/verify`'s suite run (see *Where the completion gate belongs*).
13. `/speckit.testchecklist` ← run by hand; confirms every test-plan scenario has an honest test. `/verify` does not run it, since only this route has a test plan.

### Route B — the build route (an issue that is already its own spec)
4. `/build <issue>` ← branch, RED, GREEN, refactor, docs, all committed. Unattended; ends at a green, committed branch (see *The build stage*).
5. `/study <issue>` ← record what surprised the build, if anything (see *The study pass*).

### Shared — verify, then raise
1. `/verify` ← one orchestrator: **format → full suite (the gate) → clean-context review → fix → commit.** Unattended, so it can drive a loop. Runs the PR review and slop review that used to be separate manual steps. Ends at a green, reviewed, fully-committed branch; it does **not** raise the PR.
2. `/study <issue>` ← record what surprised the verify pass, if anything.
3. `/raise-pr` ← push the branch and open the PR. Run by hand, it is a separate stage: the one irreversible, outward-facing act, and keeping it out of `/verify` makes everything before it freely re-runnable. When the stages are chained, it runs unattended too, and the deliberate human act moves to starting the chain against one named issue.

Each stage ends with a one-line verdict (see *The result-line contract*), so a script can run the build route from `/build` to `/raise-pr` with nobody in the loop (see *Running the stages end to end*).

### Periodic (not per-feature)
- **capture learnings** — fold corrections back into memory/skills. Not part of `/verify`: it needs human curation and batches better, so run it at a supervised checkpoint after several features.
- **dead-code audit** — every few features or before a release; **not per-PR**.
- **context gardening** — quarterly or after a big architectural shift.

---

## The build stage

`/build` turns one issue into a green, committed feature branch and stops. Formatting, review, pushing and the PR belong to later stages.

- **The issue is the spec.** `/build` reads the issue, not a spec folder, and implements every item of its acceptance-criteria checklist. Without a checklist, it derives the criteria from what the issue says (e.g. a bug's observed and expected behaviour) and writes them into its report for a reviewer to check. It never invents scope. It creates no spec folder, so spec-route gates find nothing to check.
- **It decides alone, and writes each decision down.** Once it has an issue, `/build` never prompts. Where the issue is ambiguous, it picks the reading that fits the existing code and the issue's intent, and records the assumption in its report — where a reviewer finds every judgement call.
- **Two named cases stop it instead.** A **public-API change** goes ahead only when the criteria require it, and is flagged in the report; a merely convenient one is not made. A **new dependency** is never added unattended: if the issue needs one, `/build` stops and says so. Both have costs beyond the branch.
- **RED first, proven by the real tool.** For production code, `/build` writes the failing tests first and must see them fail; tests that pass first time mean the behaviour exists already or the test misses the criterion. For a configuration, tooling or CI change, RED is the real tool failing before and passing after. Never write a stand-in test that reimplements a tool's check: it covers less and can pass when its own matching logic is wrong.
- **Issue labels become test markers.** Each `**Scenario: X**` label on the issue gets at least one test with a matching marker, keeping label → test traceability without the spec and test-plan steps. No labels, no markers: an invented label looks like a traceability key but traces to nothing.
- **Check the starting point before branching.** Require a clean tree and the main branch checked out, fetch the remote main, and refuse to start if local main has unpushed commits — the branch is cut from the remote, so they would silently be missing. A closed issue, or one with an open PR, also stops the run.
- **Name the branch after the issue.** Put the issue number first, after a fixed prefix, so later stages can read it from the branch: the PR stage adds the closing keyword, and the study pass files its record. A leftover branch from an earlier attempt is recreated, unless it holds commits `/build` has not inspected.
- **Commits reference the issue but never close it.** A closing keyword in a commit (`Fixes #N`, `Closes #N`) closes the issue when the commit reaches main, before review. Only the PR body closes it.
- **Run the whole suite, not just the new tests** — after RED, after GREEN and after any refactor; a regression anywhere is a failure. Updating the docs the change affects is part of the stage.
- **Leave formatting and the PR to later stages.** `/build` does not format, push or open a PR. It ends committed and clean, which is what `/verify` requires.
- **Stop-on-failure is global.** A dirty tree, an unbuildable issue, RED tests that pass, a suite that won't go green: stop at that step, report, run nothing later.
- **It ends with a verdict line:** `READY branch=<branch>` (every criterion implemented, whole suite green, tree clean) or `FAILED reason=<short reason>`. Never `READY` over a red suite, an unimplemented criterion or an uncommitted change.

---

## The verify gate

The steps between "implementation looks done" and "fit to become a PR" are a fixed sequence with one hard ordering constraint, and a human re-enacting them from prose gets it subtly wrong. `/verify` encodes them.

**The suite runs first; everything else is downstream of its exit code.** The ordering is *structural*, not policed: review cannot start on red code because it runs after the gate. Do not add a hook to enforce the ordering — the exit code **is** the gate. A hook watching for the agent *claiming* green is exactly the anti-pattern "gates attach to actions, not prose" warns against.

Properties worth copying:

- **Format first, and commit it separately.** The formatting pass runs before the suite and commits on its own, so the gate covers it and the tree is clean again before review — which the later commit of review fixes depends on.
- **Always run the whole suite — no docs-only skip.** This is the only suite run on the branch as it will be pushed: formatting and review fixes land after `/build`'s run, and a branch may not have come from `/build` at all. A skip would report a branch verified that no suite ran against.
- **Unattended by design.** No prompts, so a loop can drive it feature after feature, as well as a person. Anything needing a human turns the pipeline into a wait.
- **Stop-on-failure is global.** Dirty tree, red suite, a reviewer subagent errors: stop, report, run nothing later.
- **Cheap preconditions first** (on a feature branch? commits over main?), or a full suite and review burn before a late guard trips.
- **Review runs in clean context.** Reviewers see the diff, not the conversation that produced it. The *deciding and fixing* happens in the orchestrator's own loop; "clean context" governs the reviewing, not the fixing.
- **Validate a finding before acting on it.** Reviewer severities are fickle: cross-check a "Critical" against the actual test and spec state rather than relaying it. Acting on a mislabelled finding is how a review makes code worse.
- **Re-verify what review changed.** Post-gate fixes are unverified code: re-run the suite before reporting verified, or a bad fix reaches the PR unchecked. Then *commit* them — the PR stage pushes commits, and the gap before it is open-ended.
- **Stop before the irreversible step.** End at the verified branch; leave pushing and the PR to a separate, deliberate invocation. Everything before it is safe to re-run; the outward-facing act is not, and it is the step worth a human's decision. A chain that runs the PR stage unattended moves that decision to starting the chain.
- **Name what the review deferred.** A finding knowingly left out of scope must be named in the closing report — once the PR stage runs in a later session, that report is the only way a deferral reaches the PR body.

**Two reviews, not one.** *Review A* is synchronous inside `/verify`: clean-context subagents over the diff, whose findings stay in the conversation for `capture-learnings`. *Review B* is the asynchronous agent review on the raised PR, requested by `/raise-pr`, for a human to read at merge. Nothing waits on Review B; blocking for minutes on a second review of the same diff buys little.

---

## The study pass

`/study` records what *surprised* a piece of work — a mid-way redesign, a review finding that had to be acted on, an ambiguous criterion — as one classified row per surprise in a per-issue record. Across many issues, the rows show which part of the harness keeps costing time. It runs after `/build` and after `/verify`, and can run repeatedly. It changes no source file and blocks nothing, so a failure costs only the record.

- **Record only surprises backed by evidence.** Asked what surprised it, a model readily invents plausible surprises. Every row must point to a commit, review comment, failing test or CI run; anything else is left out.
- **A clean run writes nothing.** There is no "nothing notable" row, so a record's existence means the work taught something, and every row can be trusted.
- **"Could not look" is not "found nothing".** A run that could not gather its evidence fails rather than reporting a clean result.

---

## The result-line contract

Each stage ends with **one machine-readable verdict line** — the shared interface that lets a script chain the stages without a person reading each report:

| Stage | Success | Failure |
|---|---|---|
| `/build` | `READY branch=<branch>` | `FAILED reason=<short reason>` |
| `/study` | `STUDIED issue=<N> rows=<n>` | `FAILED reason=<short reason>` |
| `/verify` | `VERIFIED branch=<branch>` | `FAILED reason=<short reason>` |
| `/raise-pr` | `RAISED pr=<url>` | `FAILED reason=<short reason>` |

A chain starts the next stage only on the previous stage's success line, and stops at the first failure. Two rules make this safe:

- **The verdict is a structured line, not a phrase in the prose.** A report can quote something that looks like success: a PR stage failing because a PR is already open reports an error containing a valid PR URL. A chain accepting any URL would report that old PR as its result.
- **A stage prints its success line only for work it did.** `RAISED` names a PR this run opened; `READY` describes a suite this run saw pass.

---

## Running the stages end to end

Every build-route stage after the issue stage runs unattended, so a script can take one issue from a clean main branch to an open PR with no prompt: `/build` → `/study` → `/verify` → `/study` → `/raise-pr`. By hand, most of the elapsed time is waiting for a person to read each report and start the next stage; the chain removes that wait.

- **Each stage is its own headless agent process.** The script starts each as a fresh non-interactive run and reads its verdict line. Each study pass resumes the session of the stage it follows, so it studies what that stage saw; the others start fresh and inherit no context.
- **Gate on the verdict line; stop at the first failure.** The next stage starts only on the previous one's success line. A `FAILED` line, a missing or garbled verdict, an agent error or a timeout stops the chain. The closing message names the stage, the reason, and the session to reopen; stages that succeeded need not re-run.
- **The chain changes nothing itself.** It resets, cleans and pushes nothing, so the branch is as the failing stage left it. Caveat: a stage stopped by its time limit may leave background work (a test run, a subagent) still writing to the tree.
- **Check prerequisites before spending model time.** Before the first stage, confirm every tool the script needs is installed and the starting state holds (an issue named, a clean tree, main checked out), so a missing tool is named as itself rather than surfacing an hour later as a stage with no verdict. Give each stage a time limit, and offer a dry run that lists the stages and runs nothing.
- **Rely on each stage's contract rather than re-checking it.** The chain does not re-check between stages that the tree is clean or nothing was pushed early. A stage that breaks its contract is caught by the next stage's preconditions (`/verify` refuses a dirty tree), which stops the chain.
- **The human decision moves to the start.** The last stage opens the PR without a pause — a deliberate exception to *Stop before the irreversible step* in *The verify gate*. The person's decision is starting the chain against one named issue. The commands stay separate, so running `/verify` and `/raise-pr` by hand keeps the pause.
- **Residual risk: silently denied `ask` rules.** Headless stages deny `ask`-matched calls without a prompt (see *Permissions and unattended runs*), so a stage can carry on degraded and still report success; the chain cannot detect it.
- **Test the chain against a stub agent** in throwaway repositories: order, failure and timeout handling, malformed replies. It takes seconds and calls no model. Check the two things a stub cannot prove — reopening a failed stage's session, and a full real run — by hand.

NetPace's script, settings and manual checks are in the delta's [Running the chain](agentic-workflow-NetPace.md#running-the-chain).

---

## Separation of Concerns

- **Spec (what & why):** requirements as normative SHALL/MUST statements. No test scenarios.
- **Test plan (how you verify):** named scenarios derived from the complete spec, before tasks are decomposed.
- **Tasks (how you build):** implementation breakdown informed by the test plan.
- **Test checklist (did you honour it):** static analysis after implementation confirming every scenario has an honest test.

### Spec the problem, not the solution
A spec fixes *outcomes and constraints*, not mechanism. "A searchable audit log retained seven years without slowing writes" beats "create table audit_log with these eight columns" (chrismdp). As a rule: **acceptance criteria must read as user-observable outcomes that hold under any reasonable implementation.** Over-specifying the solution upfront is waterfall with new branding, and produces brittle, implementation-mirroring tests.

---

## Design Principles

- **Single branch per feature, one mission.** Tests and implementation on one branch; commit history is the audit trail. When a second mission surfaces mid-flight, ship the first with documented known issues and open a separate branch.
- **The test plan is the red-phase baseline.** In a statically-typed project, pre-implementation tests can't compile, so the committed `test-plan.md` is the locked intent and the test checklist enforces honesty.
- **PR review is the integrity gate.** The reviewer diffs the test plan and checks the checklist report — not a binary pass/fail.

### Gates over rules
A rule the agent must *remember* is weaker than a gate the system *enforces*. Karpathy notes the common failure modes (silent assumptions, overcomplication, orthogonal edits, weak success criteria) persist "despite a few simple attempts to fix it via instructions in CLAUDE.md." When a correction recurs, promote it from a written rule to a mechanical gate. **Gates attach to actions, not prose:** no hook can see the agent *say* "all tests pass", so enforcement hangs off concrete actions (a test run, a commit, a PR creation), never claims.

Three corollaries:

- **Exclusions are amendment-level, not per-PR.** When the project deliberately excludes a tool or approach (a UI-automation framework, a dependency class), record it as a *standing, named exclusion* that changes only by explicit amendment, not something an agent or a single review can reason past. Left as prose, an agent re-derives the "reasonable" case for the excluded tool every time a gate blocks it. Back it with a denylist gate so the excluded path is mechanically impossible.
- **Package operations as callable, self-documenting commands — not prose.** Anything the agent must do consistently, especially destructive or multi-step orchestration, belongs in one script/command (with real `--help` output), gated so the raw pieces can't be hand-assembled. Orchestration that lives only as prose gets re-enacted imperfectly and drifts.
- **Guard the files an upgrade will overwrite.** When a vendored scaffolding tool (spec-kit or similar) generates files carrying local customisations, a `--force` re-init silently resets them — including settings that stop an agent invoking things it shouldn't. Deny the agent's edit path on upstream-managed files and keep genuine extension points editable. The upgrade writes files directly and is unaffected: the guard stops unattended drift, not deliberate action.

---

## Mechanical enforcement layer

The *verify* and *correct* duties, made automatic. Minimum set:

- **Stale-build guard.** Block running tests `--no-build` (or equivalent) when sources changed since the last build; stale binaries produce lying green results.
- **The test-green gate.** The full suite must pass on the code about to become a PR. Put it in the verify flow, not the implement turn (see *Where the completion gate belongs*).
- **Traceability gate.** The exact-match half of the test checklist — spec label ↔ test-plan scenario ↔ code marker, character for character — is a *deterministic* gate. The judgement half (mock self-satisfaction, trivial passes, fuzzy matches) stays a human-run review command. This one *does* belong at turn-end, as a loop-guarded nudge rather than a lock-out.
- **No skipped tests.** Skipped, ignored and conditionally-skipped tests (including *runtime* skips) are banned by a static gate — they fake coverage and rot the spec→test trace. Genuinely untestable scenarios go in a documented "untested branches" table.
- **Fast/slow test categories.** Tag tests *unit* (fast, no external dependencies) or *integration* (slow, real stack) for seconds-fast inner-loop feedback. The whole suite, not the tagged subset, remains the completion gate.
- **CI on PR** *(where the suite can run in CI).* Build + test on every PR, blocking merge. If the real test stack can't run in hosted CI (heavy infra, private-repo limits), keep the full gate local, let CI cover the deterministic subset, and say so.

### Where the completion gate belongs — at verify, not at turn-end

The instinct is a turn-end hook that won't let the *implement* agent stop until the suite is green. It is the wrong seam, and the reason generalises.

A turn-end hook can't run the suite itself — a full run takes minutes. So it consults a **ledger** recording that a green run happened at some past moment. That is a *proxy*: it attests the suite passed on earlier code, not on the code about to become a PR. The plumbing it needs — ledger, file markers, locking, a "was that a whole-suite or filtered run?" heuristic — is substantial, and buys a weaker attestation than the one you wanted.

**Put the gate where the truth is: a real whole-suite run at verify time, on exactly the diff about to become a PR.** It removes more machinery than it adds, and the guarantee gets *stronger* — the suite passes **now**, on the diff under review. During implementation, keeping the suite green is a soft standard at the agent's discretion, where discretion is cheap and a hard gate is merely a tax.

The general rule: **when a gate can only see a proxy for the property you care about, move the gate to where the property is observable.** A gate on a ledger is a gate on a claim about the past — a short step from the "gates attach to actions, not prose" failure it was meant to avoid.

### How many green runs? One — and fix the flakiness instead

A tempting bar is *N consecutive* green runs, reasoning that on a non-deterministic stack one green run is luck. Resist it. **A multi-run bar is a crutch for a flaky suite, and prices every completion at N× the suite's wall-clock.** "We need three green runs to believe it" means "our suite lies one run in three"; the fix is the flakiness, not the arithmetic.

Set the bar at **one green whole-suite run since the last code change**, and *earn* it: if the suite is non-deterministic, hunt the flake. Retire an existing multi-run rule on evidence, not taste — a provocation campaign (repeated runs under varied seed, concurrency, accumulated state and CPU pressure) either demonstrates determinism, retiring the rule, or surfaces the flake you needed to find.

### Formatting is not verification — do it at verify cadence

Formatting is cosmetic and doesn't belong on the inner loop. A format-on-commit hook taxes **every** commit — on a real codebase the tool's workspace load takes tens of seconds — to fix what no reviewer would catch. Run it once per PR in the verify flow, where the cadence already costs minutes. (Boris Cherny's "formatting handles the last 10%" is right about the value and silent about the cadence; per-commit is the wrong one.)

> Pre-allow safe commands in checked-in settings rather than disabling permission prompts wholesale (Boris Cherny): the agent flows, but high-stakes actions still surface.

### Permissions and unattended runs

Claude Code's permission rules behave differently when nobody is there to answer a prompt; design unattended stages around that.

- **`ask` rules outrank every permission mode.** `bypassPermissions` does not clear them; only removing the rule does. Subagents inherit the parent's mode — "if the parent uses `bypassPermissions` or `acceptEdits`, this takes precedence and can't be overridden" — so a reviewer subagent stopped on a permission has hit an `ask` rule, not the subagent boundary.
- **Headless runs deny `ask` rules silently.** An `ask`-matched call prompts interactively, but a headless `claude -p` run can't prompt, so the call is denied and the worker carries on degraded. A green unattended run is not evidence its `ask` rules did no harm. If an unattended stage genuinely needs a command, give it an `allow` rule.
- **Headless runs show which rule matched, and nothing more.** Under `claude -p --dangerously-skip-permissions`, every call an `allow`, `ask` or `deny` rule would stop comes back denied, with no person to mask it — a test of rule matching. Its blind spot: approvals that need an interactive screen don't fire headlessly. A recursive `grep` whose read scope overlaps a `Read(…)` deny rule prompts interactively but runs clean under `claude -p`, a false all-clear. It answers "which rule matched", not "would a person have been asked". It can't run in CI either: it needs the `claude` binary and a signed-in session.

---

## Context management

Output quality follows a curve (chrismdp): too little context gives generic slop; too much and the model drowns in noise and is confidently vague. Two habits:
- **Reset on drift.** Useless refactoring of working code, repeated wrong assumptions or going in circles mean start a fresh session: resetting is cheaper than correcting, and models tend to stay lost once they take a wrong turn.
- **Plan mode first for non-trivial work** (Boris Cherny / Karpathy): agree the plan before auto-accepting edits. A good plan is often the difference between a one-shot and a spiral. Reserve the heavyweight spec pipeline for features; use lightweight plan mode for small changes.

For **long-running / multi-session** work (Anthropic): keep a durable progress ledger and a feature list seeded as "all failing", so a later session gets its bearings from git history and progress notes rather than re-deriving state, and can't declare premature victory.

**Prefer the tracker's own state as that ledger.** Before building a progress file, check what the issue tracker already models: open/closed, dependencies, labels and PR links are durable, shared, human-visible, and already what the team edits. A modern tracker resolves the dependency graph server-side, so "what is ready now" is a *query*, not a parser and graph walk you maintain. A bespoke ledger has to be kept true; the tracker is true by construction, and every hand-rolled mirror eventually disagrees with it.

Two practices follow:

- **Let the labels be the state.** When an automated step moves an issue forward, record where it stands in labels and the issue body, readable at a glance. Give every combination exactly one meaning, including "the run did not finish", so the state reads off the issue without checking run logs.
- **The workflow checks its own results rather than trusting what the agent says.** An agent run usually reports success whenever the model finishes its turn, whatever it did. Check the post-conditions that matter (a label removed, a comment posted) and fail the run when they don't hold.

---

## The feedback loop (compounding engineering)

The trainer's job is to make next week's agent better than this week's:
- **Capture corrections as durable artifacts** — project-specific rules into standing instructions / memory; generic rules into reusable skill files (chrismdp).
- **Do it continuously.** The strongest teams add to their shared instructions *several times a week*, the moment the agent goes wrong (Boris Cherny — "compounding engineering"). In review, tag the agent on the PR to fold the lesson in.
- **Ship one skill file a week** as a baseline (chrismdp): pick a recurring annoyance and formalise it.
- **Prefer a gate to a memory entry** where the learning can be enforced mechanically. A correction that *could* have been a hook is a rule you will re-teach.

---

## Documentation Hierarchy

- Tier 1: GOVERNANCE → constitution / principles (supersedes all else)
- Tier 2: IMPLEMENTATION → `CLAUDE.md` / `AGENTS.md` (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → `docs/…` deep guidance and/or a memory corpus

Keep Tier 2 short and high-signal; symlink `CLAUDE.md`↔`AGENTS.md` so every tool reads the same rules. Tier 3 may be convention docs, a memory ledger, or both — record which in the project's delta doc.

---

## Failure modes the harness defends against

| Failure mode (Karpathy / chrismdp) | Harness defence |
|---|---|
| Silent assumptions, no clarifying questions | plan-mode first; review/confirm gates surface decisions before code |
| Overcomplication, bloated abstractions | slop review + simplifier sub-agent; "would a senior call this overcomplicated?" |
| Orthogonal edits (touching unrelated code) | one-mission branch; "mention dead code, don't delete it"; diff-scoped review |
| Weak success criteria | outcome-level ACs + test plan + the verify gate (a real suite run before the PR) |
| Accidental vibe coding (ship unverified) | the mechanical enforcement layer; the verify gate's structural ordering |
| Review fatigue | move recurring issues into skill files / gates, off the human's plate |
| Harness change locks out the harness | build gates fail-open with an override first, verify, then tighten |

---

## Appendix — Codebase Setup

The kinds of files a project adds to make this workflow operational (names illustrative):

### Agent configuration (`.claude/` or `.agents/`)
- **settings** — checked-in permissions allowlist + hooks: *stale-build guard*, *traceability nudge*, any denylist gates backing a standing exclusion, and a deny path over upstream-managed vendored files. Not here: the test-green gate, which is a real suite run inside `/verify`. The one nearby hook re-runs the suite at `gh pr create`, which belongs to the PR stage.
- **commands** — the slash commands above: `draftissue`, `reviewissue` and `confirmissue` (issue stage); `testplan` and `testchecklist` (spec route); `build` (build route); the `verify` orchestrator for the pre-PR steps; `raise-PR`, the separate stage after it; and `study`, run after build and after verify. Plus maintenance commands: slop review, dead-code audit, context-gardening, capture-learnings, `bugmagnet` (systematic test-coverage and edge-case discovery for one module) and `install-harness-tooling` (installs the token/context plugins below).
- **skills / sub-agents** — simplifier, verifier, a `diagnose` skill (a reproduce → minimise → hypothesise → instrument → fix → regression-test loop for hard bugs), and any stack-orchestration script.

### spec-kit configuration (`.specify/`)
- **constitution** — project governance, versioned, supersedes other guides.
- **extensions** — lifecycle hooks (git auto-commit per phase; test-plan cross-check).

### CI / SCM integration
- **CI workflow** — build + test on PRs (full suite or deterministic subset; see enforcement).
- **Agent review action** — auto-review on agent-authored PRs; respond to `@agent` mentions.
- **PR template** — Summary, Spec link, Changed files, New artifacts.

### Conventions / reference
- Detailed style guide; optional Change-Intent-Records (or an equivalent decision ledger).

---

## Modifying the harness itself

Harness changes can destroy the tool making the next change — a gate that blocks its own `git commit` is not hypothetical. Four rules:

1. **Test every hook standalone with synthetic input** before wiring it into settings. Every hook is a tested script with its own test cases, never an inline one-liner in settings: an untested gate can silently do nothing (a bad path, a missing argument), which looks the same as a gate that passed. Only **exit code 2** blocks a Claude Code `PreToolUse` hook; any other non-zero exit is reported and ignored, so a blocking gate must exit 2.
2. **Build fail-open, with an override, first.** Verify no lock-out, *then* tighten to fail-closed.
3. **One mission per branch, its own PR.** Dogfood the workflow once the gate exists.
4. **A human reviews each hook before it lands in settings.** Non-negotiable.

---

## Token / context management plugins
- **read-once** — avoid re-reading unchanged files.
- **context-mode** — context budgeting.
- **rtk** — token-saving CLI proxy (`rtk gain`).

**Nothing checks that these are installed.** Config can declare, allow and enable a tool that is missing from the machine, and a missing tool fails silently — the session just loses the savings. Add a report that checks each declared tool is installed and reachable. Where it can't reach an answer it should say `unknown`, not `no`: a truthful yes/no report must not turn a failed check into an answer.
