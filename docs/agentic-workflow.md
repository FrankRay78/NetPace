<!--
Provenance: this is the GENERIC, stack-portable workflow guide — deliberately stack-neutral so it
stays portable and reusable. Keep it that way: NetPace-specific behaviour belongs in the delta doc
alongside it, agentic-workflow-NetPace.md, not here.
-->

# Agentic Software Development Workflow

## Introduction

Writing a spec before touching code, locking a test plan before writing a test, and **enforcing both mechanically** — that is the discipline this workflow encodes. The result is a *harness*: an agent (Claude Code, Codex CLI, …) constrained by context, feedback loops, and automated quality gates so that the agent does the work and the engineer reviews it.

**The workflow in one line:** draft, review and confirm an issue → build it by one of two routes (the full spec route for a large feature: spec → test plan → tasks → implement; or the lighter build route for an issue that is already its own spec) → verify → raise the PR → merge.

This document is **stack-generic**. A concrete project implements it by adding the files in the [Appendix](#appendix--codebase-setup) and recording any project-specific deviations in a short companion "implementation delta" doc — keeping this guide as the single shared source of truth across repos.

*Inspired by:*
- [Coding with AI](https://www.chrismdp.com/coding-with-ai/) — Chris Parsons, 2026.
- [Harness Engineering](https://openai.com/index/harness-engineering/) — OpenAI, 2026.
- [Effective harnesses for long-running agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) — Anthropic, 2025.
- Field notes — Andrej Karpathy (Dec 2025) and Boris Cherny (Feb 2026).

### Which route do I use?

Every piece of work starts as an issue that has been drafted, reviewed and confirmed. What happens next depends on whether that issue can serve as the spec:

- **Use the build route** when the confirmed issue already states the behaviour you want as acceptance criteria that can be checked — a bug with observed and expected behaviour, a small feature, a docs or tooling change. `/build` works straight from the issue, and the issue is the only planning document.
- **Use the full spec route** when the work is too large or too uncertain to fit in an issue — several user flows, open design questions, or a change that needs a test plan agreed before any code exists. The spec, test plan and task list give you checkpoints to review along the way, which the build route does not.

When in doubt, try the build route first. If `/build` reports that the issue does not settle the design, that tells you the issue needs a spec.

---

## Why a harness, not better prompts

When an agent struggles with trivial work, treat it as a **context/harness failure, not a prompt failure** (chrismdp). The model is rarely the bottleneck — GitHub Copilot running a frontier model underperforms a purpose-built CLI harness, because the wrapper (context selection, the agent loop, the gates) matters at least as much as the model.

A useful frame is the **five duties of a harness** (OpenAI): the harness must **constrain** what the agent may do, **inform** it of what it should do, **verify** its work, **correct** its mistakes, and **keep humans in the loop at high-stakes decisions.** Every section below maps to one of these duties — and the duty teams most often under-build is *verify*.

**The bottleneck has moved from generation to verification** (chrismdp). The question is no longer "how fast can we build?" but "how fast can we tell if it's right?" Two practical consequences run through this workflow:
- If verifying an AI change takes as long as writing it yourself, either present it differently, move the verification to an automated gate, or don't delegate that task.
- **Give the agent a way to verify its own work** — a test loop, a type checker, a browser check. With that feedback loop in hand, output quality rises sharply (Boris Cherny: ~2–3×).

---

## Workflow Execution Order

> Slash-command names below are the reference Claude-Code/spec-kit set; a project may rename
> them. The *sequence* is the contract, not the names.

Every feature goes through one shared issue stage, then one of two routes (see *Which route do I use?*), and both routes finish the same way.

### Shared — the issue stage (on the main branch)
1. `/speckit.draftissue` ← optional; turn an unstructured brief into a well-formed issue
2. `/speckit.reviewissue` ← pre-spec gate; posts gaps + recommendations as an issue comment
3. `/speckit.confirmissue` ← fold answered review into a `## Confirmed decisions` section, and label the issue `ready`

### Route A — the full spec route (large features)
4. `/speckit.specify`
5. `/speckit.clarify` ← iterate until the spec feels complete
6. `/speckit.checklist` ← resolve all gaps before continuing
7. `/speckit.plan`
8. `/speckit.testplan` ← review output carefully before continuing
9. *red-phase commit* ← commit `test-plan.md` as locked intent (script per project)
10. `/speckit.tasks`
11. `/speckit.analyze` ← resolve HIGH/CRITICAL before branching; runs the test-plan cross-check via the `after_analyze` hook
12. `/speckit.implement` ← agent runs to suite-green, keeping the suite green on the inner loop at its own discretion. This is a **soft standard, not a per-turn gate** — the binding "green before a PR" guarantee is the real suite run in `/verify` (see *Where the completion gate belongs*).
13. `/speckit.testchecklist` ← run by hand; confirms every test-plan scenario has an honest test. `/verify` does not run it, because only this route has a test plan to check against.

### Route B — the build route (an issue that is already its own spec)
4. `/build <issue>` ← branch, RED tests, GREEN, refactor, docs, all committed. Runs unattended and ends at a green, committed branch (see *The build stage*).
5. `/study <issue>` ← record what surprised the build, if anything (see *The study pass*).

### Shared — verify, then raise
1. `/verify` ← one orchestrator: **format → full suite (the gate) → clean-context review → fix → commit.** Runs unattended, so it can drive a loop. It runs the PR review and the slop review, which used to be separate manual steps. Ends at a green, reviewed, fully-committed branch — it does **not** raise the PR.
2. `/study <issue>` ← record what surprised the verify pass, if anything.
3. `/raise-pr` ← push the branch and open the PR. Run by hand, it is a separate stage: it is the one irreversible, outward-facing act in the sequence, and keeping it out of `/verify` is what makes everything before it freely re-runnable. A project may chain the stages end to end so that this step also runs unattended. The deliberate human act then moves to starting the chain against one named issue, and the project records that exception in its delta doc (for this repository, [Running the chain](agentic-workflow-NetPace.md#running-the-chain)).

Each stage ends with a one-line verdict, so the stages can be chained — see *The result-line contract*.

### Periodic (not per-feature)
- **capture learnings** — fold corrections back into memory/skills. Deliberately *not* part of `/verify`: it needs human curation and batches better across several features, so run it at a supervised checkpoint after a batch.
- **dead-code audit** — every few features or before a release; **not per-PR**.
- **context gardening** — quarterly or after a big architectural shift.

---

## The verify gate

`/verify` exists because the steps between "implementation looks done" and "this branch is fit to become a PR" are a fixed sequence with one hard ordering constraint, and a human re-enacting them from prose gets it subtly wrong.

**The suite runs first and everything else is downstream of its exit code.** This ordering is *structural*, not policed: review cannot begin against unverified or red code because it literally runs after the gate. Do not add a hook to enforce the ordering — the exit code **is** the gate. A hook that watches for the agent *claiming* green is exactly the anti-pattern the "gates attach to actions, not prose" rule warns against.

Properties worth copying:

- **Unattended by design.** No prompts anywhere in the flow, so `/verify` can be driven by an automated loop working features back-to-back, as well as invoked by hand. Anything that needs a human turns the pipeline into a wait.
- **Stop-on-failure is global.** Dirty tree, suite not green, a reviewer subagent errors — stop at that step, report, and run nothing later.
- **Preconditions run before the expensive work.** Check the cheap things first (on a feature branch? any commits over main?), or a full suite and full review burn before a late guard trips.
- **Review runs in clean context.** Reviewers see the diff, not the conversation that produced it. The *deciding and fixing* legitimately happens in the orchestrator's own loop — "review in clean context" governs the reviewing, not the fixing.
- **Validate a finding before acting on it.** Reviewer severities are fickle; cross-check a "Critical" against the actual test and spec state rather than relaying it verbatim. Acting on a mislabelled finding is how a review pass makes code worse.
- **Re-verify what review changed.** Fixes applied after the gate are unverified code — re-run the suite before reporting the branch verified, or a bad fix reaches the PR green-unchecked. Then *commit* the fixes: the PR stage pushes commits, and because it is a separate stage the gap between the fix and the push is open-ended.
- **Stop before the irreversible step.** End at the verified branch and leave pushing and opening the PR to a separate deliberate invocation. Everything up to that point is safe to re-run; the outward-facing act is not, and it is the one step worth a human's decision. A chain that runs the PR stage unattended does not remove that decision. It moves it to starting the chain.
- **Name what the review deferred.** A finding consciously left as out-of-scope must be named in the closing report. Once the PR stage runs in a later session, that report is the only route by which a deferral reaches the PR body — an unnamed one is simply lost.

**Two reviews, not one.** *Review A* is synchronous and inside `/verify` — clean-context subagents over the diff, whose findings are in-conversation and therefore available to `capture-learnings` later. *Review B* is the asynchronous agent review on the raised PR, requested by `/raise-pr`, for a human to read at merge. Nothing waits on Review B: blocking a pipeline for minutes to fold in a second review of the same diff buys little.

---

## The build stage

`/build` is the build route's implementation step. It turns one issue into a green, committed feature branch, and it stops there. Formatting, review, pushing and the PR belong to the stages after it.

- **The issue is the spec.** `/build` reads the issue, not a spec folder. If the issue has an acceptance-criteria checklist, `/build` implements every item on it. If it has none, `/build` works out the criteria from what the issue actually says, such as the observed and expected behaviour of a bug, and writes them into its report so a reviewer can check that reading. It does not invent scope to fill a gap. It creates no spec folder either, so the spec-route gates that read one find nothing and have nothing to check.
- **It decides alone, and writes each decision down.** Once it has an issue, `/build` runs without prompting. Where the issue is ambiguous, it picks the reading that fits the existing code and the issue's stated intent, and records that assumption in its final report. The report is where a reviewer finds every judgement call the agent made.
- **Two named cases stop it instead.** A **public-API change** goes ahead only when the issue's criteria require it, and the report flags it for review; a change that is merely convenient is not made. A **new dependency** is never added unattended. If the issue cannot be built without one, `/build` stops and reports that. Both are decisions with costs beyond the branch, so the agent does not make them.
- **RED first, and the real tool proves it.** For production code, `/build` writes the failing tests before any implementation and must see them fail. If they pass on the first run, either the behaviour already exists or the test does not exercise the criterion. For a configuration, tooling or CI change, the RED step is the real tool failing before the change and passing after it. `/build` never writes a stand-in test that reimplements a check a tool already makes, because such a test covers less than the tool and can pass when its own matching logic is wrong.
- **Issue labels become test markers.** If the issue carries `**Scenario: X**` labels, each one gets at least one test with a matching marker. This keeps the label → test traceability chain with the spec and test-plan steps left out, since the issue is the spec. If the issue has no labels, the tests get no markers. An invented label looks like a traceability key but traces to nothing.
- **Stop-on-failure is global.** A dirty tree, an issue that cannot be built as written, RED tests that do not fail, a suite that will not go green: `/build` stops at that step, reports it, and runs nothing later.
- **It ends with a verdict line.** The last line is either `READY branch=<branch>`, meaning every criterion is implemented, the whole suite is green and the tree is clean, or `FAILED reason=<short reason>`. `/build` never prints `READY` over a red suite, an unimplemented criterion or an uncommitted change.

---

## The study pass

`/study` records what *surprised* a piece of work, such as a redesign mid-way through, a review finding that had to be acted on, or a criterion that turned out to be ambiguous. Each surprise becomes one classified row in a per-issue record, and over many issues those rows show which part of the harness keeps costing time. It runs after `/build` and after `/verify`, and it can be run more than once. It changes no source file and never blocks another stage, so if it fails, the only loss is the record.

- **Record only surprises backed by evidence.** Asked what surprised it, a model will readily invent plausible surprises. Every row must point to something the run can show: a commit, a review comment, a failing test or a CI run. A finding that cannot be traced to evidence is left out, however plausible it sounds.
- **A clean run writes nothing.** No record says "nothing notable". Because no record is filler, the existence of a record means the work taught something, and a reader can trust every row in it.
- **"Could not look" is not "found nothing".** A run that could not gather its evidence fails. It does not report a clean result.

---

## The result-line contract

Each stage ends with **one machine-readable verdict line**. These lines are the shared interface that lets a script chain the stages without a person reading each report:

| Stage | Success | Failure |
|---|---|---|
| `/build` | `READY branch=<branch>` | `FAILED reason=<short reason>` |
| `/study` | `STUDIED issue=<N> rows=<n>` | `FAILED reason=<short reason>` |
| `/verify` | `VERIFIED branch=<branch>` | `FAILED reason=<short reason>` |
| `/raise-pr` | `RAISED pr=<url>` | `FAILED reason=<short reason>` |

A chain starts the next stage only after the previous stage prints its success line, and it stops at the first failure. Two rules make this safe:

- **The verdict is a structured line, not a phrase found in the prose.** A report can quote a string that looks like success. The obvious case is a PR stage that fails because a PR is already open, whose error message contains a valid PR URL. A chain that accepted any URL would report that old PR as its result.
- **A stage prints its success line only for work it actually did.** `RAISED` names a PR that this run opened, and `READY` describes a suite that this run saw pass.

---

## Separation of Concerns

- **Spec (what & why):** requirements as normative SHALL/MUST statements. No test scenarios.
- **Test plan (how you verify):** named scenarios derived from the spec, after the spec is complete, before tasks are decomposed.
- **Tasks (how you build):** implementation breakdown informed by the test plan.
- **Test checklist (did you honour it):** static analysis after implementation confirming every scenario has an honest test.

### Spec the problem, not the solution
A spec fixes *outcomes and constraints*, not mechanism. "We need a searchable audit log retained seven years without slowing writes" beats "create table audit_log with these eight columns" (chrismdp). The same idea, stated as a rule: **acceptance criteria must read as user-observable outcomes that hold under any reasonable implementation.** Over-specifying the solution upfront is the waterfall mistake with new branding, and it produces brittle, implementation-mirroring tests.

---

## Design Principles

- **Single branch per feature, one mission.** Tests and implementation on the same branch; commit history is the audit trail. When a second mission surfaces mid-flight, ship the first with documented known-issues and open a separate branch.
- **The test plan is the red-phase baseline.** In a statically-typed project, pre-impl test files can't compile; the committed `test-plan.md` is the locked intent, and the test checklist enforces honesty.
- **PR review is the integrity gate.** The reviewer diffs the test plan and checks the checklist report — not a binary pass/fail.

### Gates over rules
A rule the agent must *remember* is weaker than a gate the system *enforces* — Karpathy notes the common failure modes (silent assumptions, overcomplication, orthogonal edits, weak success criteria) persist "despite a few simple attempts to fix it via instructions in CLAUDE.md." When the same correction recurs, promote it from a written rule to a mechanical gate. **Crucially, gates attach to actions, not prose** — no hook can see the agent *say* "all tests pass," so enforcement must hang off concrete actions (a test run, a commit, a PR creation), never off claims.

Three corollaries:

- **Exclusions are amendment-level, not per-PR.** When the project deliberately excludes a tool or approach (a UI-automation framework, a whole dependency class), record it as a *standing, named exclusion* that changes only by explicit amendment — not a call an agent or a single review can reason past mid-task. Left as prose guidance, an agent will re-derive the "reasonable" case for the excluded tool every time a gate blocks it. Back the exclusion with a denylist gate so the excluded path is mechanically impossible.
- **Package operations as callable, self-documenting commands — not prose.** Anything you want the agent to do consistently, especially destructive or multi-step orchestration, belongs in a single script/command it invokes (with real `--help` output it can query), gated so the raw pieces can't be hand-assembled. Orchestration that lives only as prose in a skill or doc gets re-enacted imperfectly and drifts every time.
- **Guard the files an upgrade will overwrite.** Where the project vendors a scaffolding tool (spec-kit or similar) whose generated files carry local customisations, a `--force` re-init silently resets them — including the settings that stop an agent invoking things it shouldn't. Deny the agent's edit path on upstream-managed files so drift can't accumulate there, and keep genuine extension points editable. The upgrade itself writes files directly and is unaffected, which is the point: the guard stops unattended drift, not deliberate action.

---

## Mechanical enforcement layer

The *verify* and *correct* duties, made automatic. Minimum set:

- **Stale-build guard.** Block running tests `--no-build` (or equivalent) when sources changed since the last build — stale binaries produce lying green results.
- **The test-green gate.** The full suite must pass on the code about to become a PR. Put this in the verify flow, not the implement turn — see *Where the completion gate belongs*, below.
- **Traceability gate.** The exact-match half of the test checklist — spec label ↔ test-plan scenario ↔ code marker, character-for-character — is a *deterministic* gate. The judgment-level half (mock self-satisfaction, trivial-pass, fuzzy matches) stays a human-run review command. This one *does* belong at the agent's turn-end, as a loop-guarded nudge rather than a lock-out.
- **No skipped tests.** Skipped / ignored / conditionally-skipped tests (including *runtime* skips) are banned by a static gate — they fake coverage and rot the spec→test trace. Genuinely-untestable scenarios go in a documented "untested branches" table, not a faked skipped test.
- **Fast/slow test categories.** Tag tests *unit* (fast, no external dependencies) vs *integration* (slow, real stack), so the agent gets seconds-fast inner-loop feedback while developing — the whole-suite run remains the completion gate, not the tagged subset.
- **CI on PR** *(where the suite can run in CI).* Build + test on every PR, blocking merge. If the real test stack can't run in hosted CI (heavy infra, private-repo limits), keep the full gate local and let CI cover the deterministic subset only — and say so explicitly.

### Where the completion gate belongs — at verify, not at turn-end

The instinct is to gate the *implement* agent: a turn-end hook that refuses to let it stop until the suite is green. It is the wrong seam, and the reason generalises.

A turn-end hook cannot run the suite itself — a full run is minutes, and the agent's turn is not the place to spend them. So it does the only thing it can: it consults a **ledger** recording that a green run happened at some past moment, and gates on that. But a past green run is a *proxy*. It attests that the suite passed on some earlier state of the code, not that it passes on the code about to become a PR. The plumbing this proxy needs — the ledger, the file markers, the locking, the "was that a whole-suite run or a filtered one?" heuristic — is substantial, and it buys an attestation weaker than the thing you actually wanted.

**Put the gate where the truth is: a real whole-suite run at verify time, on exactly the diff that is about to become a PR.** It is more machinery removed than added, and the guarantee gets *stronger* — the suite passes **now**, on exactly the diff under review. During implementation, keeping the suite green becomes a soft standard the agent applies at its own discretion on the inner loop, which is where discretion is cheap and a hard gate is merely a tax.

The general rule this instance teaches: **when a gate can only see a proxy for the property you care about, move the gate to where the property itself is observable.** A gate on a ledger is a gate on a claim about the past, which is a short step from the "gates attach to actions, not prose" failure it was meant to avoid.

### How many green runs? One — and fix the flakiness instead

A tempting bar is *N consecutive* green runs, on the reasoning that a non-deterministic stack makes a single green run luck rather than proof. Resist it. **A multi-run bar is a crutch for a flaky suite, and it prices every completion at N× the suite's wall-clock.** The honest reading of "we need three green runs to believe it" is "our suite lies one run in three" — and the fix for that is the flakiness, not the arithmetic.

Set the bar at **one green whole-suite run since the last code change**, and *earn* it: if the suite is genuinely non-deterministic, hunt the flake. Where a multi-run rule already exists, retire it against evidence rather than taste — a provocation campaign (repeated runs under varied seed, concurrency, accumulated state, and CPU pressure) either demonstrates determinism, which retires the rule, or surfaces the flake, which is the thing you actually needed to find.

### Formatting is not verification — do it at verify cadence

Formatting is cosmetic, and cosmetic work does not belong on the inner loop. A format-on-commit hook taxes **every** commit — on a real codebase the tool's workspace load is measured in tens of seconds — to fix something no reviewer would have caught anyway. Fold the format step into the verify flow instead, where it runs once per PR at a cadence that already costs minutes. (Boris Cherny's "formatting handles the last 10%" is right about the value and silent about the cadence; per-commit is the wrong one.)

> Pre-allow safe commands in checked-in settings rather than disabling permission prompts
> wholesale (Boris Cherny): the agent flows, but high-stakes actions still surface.

### Permissions and unattended runs

Claude Code's permission rules behave differently when nobody is there to answer a prompt, and an unattended stage has to be designed around that.

- **`ask` rules take priority over every permission mode.** `bypassPermissions` does not clear them, and the only way to clear one is to remove the rule. Subagents inherit the parent's mode — "if the parent uses `bypassPermissions` or `acceptEdits`, this takes precedence and can't be overridden" — so a reviewer subagent that stops on a permission has hit an `ask` rule, not the subagent boundary.
- **Headless runs deny `ask` rules without saying so.** An `ask`-matched call prompts in an interactive session. In a headless `claude -p` run, which cannot show a prompt, the call is simply denied: the worker loses that capability and carries on with less. So a green unattended run is not evidence that its `ask` rules did no harm. Where an unattended stage genuinely needs a command, the fix is an `allow` rule.
- **Headless runs show which rule matched, and nothing more.** Run a workflow under `claude -p --dangerously-skip-permissions` and every call that an `allow`, `ask` or `deny` rule would have stopped comes back denied, with no person there to hide it. That makes the run a way to test rule matching. It has a blind spot: approvals that need an interactive screen do not fire headlessly at all. For example, a recursive `grep` whose read scope overlaps a `Read(…)` deny rule prompts in an interactive session but runs cleanly under `claude -p`, so the headless run gives a false all-clear. It answers "which rule matched", not "would a person have been asked". It also cannot run in CI, because it needs the `claude` binary and a signed-in session.

---

## Context management

Output quality follows a curve (chrismdp): too little context → generic slop; too much → the model drowns in noise and is confidently vague. Two habits:
- **Reset on drift.** Useless refactoring of working code, repeated wrong assumptions, or a "going in circles" feel are signs to start a fresh session — resetting is cheaper than correcting, and models tend to stay lost once they take a wrong turn.
- **Plan mode first for non-trivial work** (Boris Cherny / Karpathy): agree the plan before switching to auto-accept edits. A good plan is often the difference between a one-shot and a spiral. Reserve the heavyweight spec pipeline for features; use lightweight plan-mode for small changes.

For **long-running / multi-session** work (Anthropic): keep a durable progress ledger and a feature list seeded as "all failing," so a later session gets its bearings from git history + progress notes rather than re-deriving state — and can't declare premature victory.

**Prefer the tracker's own state as that ledger.** Before building a progress file, check what the issue tracker already models: issue open/closed, dependency relationships, labels, and PR links are durable, shared, human-visible, and already the thing the team edits. A modern tracker resolves the dependency graph server-side and reports it per issue, so "what is ready to work on now" is a *query*, not a parser and a graph walk you maintain. A bespoke ledger has to be kept true; the tracker is true by construction, and every hand-rolled mirror of it eventually disagrees with it.

Two practices follow from this:

- **Let the labels be the state.** When an automated step moves an issue forward, record where it stands in labels and in the issue body, which a person can read at a glance. Define every combination to mean exactly one thing, including "the run did not finish", so that anyone can read the state off the issue without checking run logs.
- **The workflow checks its own results rather than trusting what the agent says.** An automated agent run usually reports success whenever the model finishes its turn, whatever the model actually did. The workflow around it should check the post-conditions that matter, such as a label removed or a comment posted, and fail the run when they do not hold.

---

## The feedback loop (compounding engineering)

The trainer's job is to make next week's agent better than this week's:
- **Capture corrections as durable artifacts** — project-specific rules into the standing instructions / memory; generic rules into reusable skill files (chrismdp).
- **Do it continuously.** The strongest teams add to their shared instructions *multiple times a week*, the moment the agent does something wrong (Boris Cherny — "compounding engineering"). During review, tag the agent on a PR to fold the lesson in as part of the PR.
- **Ship one skill file a week** as a baseline cadence (chrismdp): pick a recurring annoyance, formalise it.
- **Prefer a gate to a memory entry** where the learning can be mechanically enforced. A captured correction that *could* have been a hook is a rule you will re-teach.

---

## Documentation Hierarchy

- Tier 1: GOVERNANCE          → constitution / principles (supersedes all else)
- Tier 2: IMPLEMENTATION      → `CLAUDE.md` / `AGENTS.md` (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → `docs/…` deep guidance and/or a memory corpus

Keep Tier 2 short and high-signal; symlink `CLAUDE.md`↔`AGENTS.md` so every tool reads the same rules. A project may realise Tier 3 as convention docs, a memory ledger, or both — record which in the project's delta doc.

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
- **settings** — checked-in permissions allowlist + hooks: *stale-build guard*, *traceability nudge*, plus any denylist gates backing a standing exclusion; and a deny path over upstream-managed vendored files. Note what is *not* here: the test-green gate is a real suite run inside `/verify`, not a hook. The one hook nearby re-runs the suite at `gh pr create` time, which belongs to the PR stage.
- **commands** — the custom slash commands above: `draftissue`, `reviewissue` and `confirmissue` for the issue stage; `testplan` and `testchecklist` for the spec route; `build` for the build route; the `verify` orchestrator, which composes the pre-PR steps; `raise-PR`, which follows it as a separate stage; and `study`, which records surprises after build and after verify. Alongside them sit the maintenance commands: slop review, dead-code audit, context-gardening, capture-learnings, `bugmagnet` (systematic test-coverage and edge-case discovery for one module) and a tooling installer (`install-harness-tooling`) for the token/context plugins below.
- **skills / sub-agents** — simplifier, verifier, a `diagnose` skill (a disciplined reproduce → minimise → hypothesise → instrument → fix → regression-test loop for hard bugs), and any stack-orchestration script.

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

Harness changes are the one class of work that can destroy the tool making the next change — a gate that blocks its own `git commit` is not a hypothetical. Four rules:

1. **Test every hook standalone with synthetic input** before wiring it into settings. Every hook is a tested script with its own set of test cases, never an inline one-liner in settings. An untested gate can quietly do nothing — a bad path or a missing argument makes it exit without doing its job — and from the outside that looks the same as a gate that passed. Also, only **exit code 2** blocks a Claude Code `PreToolUse` hook. Any other non-zero exit is reported and then ignored, so a gate that is meant to block must exit with 2.
2. **Build fail-open, with an override, first.** Verify no lock-out, *then* tighten to fail-closed.
3. **One mission per branch, its own PR.** Dogfood the workflow once the gate exists.
4. **A human reviews each hook before it lands in settings.** Non-negotiable.

---

## Token / context management plugins
- **read-once** — avoid re-reading unchanged files.
- **context-mode** — context budgeting.
- **rtk** — token-saving CLI proxy (`rtk gain`).

**Nothing checks that these are installed.** Config can declare a tool, allow its commands and enable it as a plugin while the tool itself is missing from the machine. A missing tool fails without any error: the session just loses the savings. Add a report that checks each declared tool is actually installed and reachable. Where the report cannot reach an answer, it should say `unknown`, not `no`. A report whose job is a truthful yes or no must not turn a failed check into an answer.
