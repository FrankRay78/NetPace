<!--
Provenance: this is the GENERIC, stack-portable workflow guide — deliberately stack-neutral so it
stays portable and reusable. Keep it that way: NetPace-specific behaviour belongs in the delta doc
alongside it, agentic-workflow-NetPace.md, not here.
-->

# Agentic Software Development Workflow

## Introduction

Writing a spec before touching code, locking a test plan before writing a test, and **enforcing both mechanically** — that is the discipline this workflow encodes. The result is a *harness*: an agent (Claude Code, Codex CLI, …) constrained by context, feedback loops, and automated quality gates so that the agent does the work and the engineer reviews it.

**The workflow in one line:** write a rigorous spec → generate a test plan → review it → generate tasks → implement → verify test coverage → PR review → merge.

This document is **stack-generic**. A concrete project implements it by adding the files in the [Appendix](#appendix--codebase-setup) and recording any project-specific deviations in a short companion "implementation delta" doc — keeping this guide as the single shared source of truth across repos.

*Inspired by:*
- [Coding with AI](https://www.chrismdp.com/coding-with-ai/) — Chris Parsons, 2026.
- [Harness Engineering](https://openai.com/index/harness-engineering/) — OpenAI, 2026.
- [Effective harnesses for long-running agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) — Anthropic, 2025.
- Field notes — Andrej Karpathy (Dec 2025) and Boris Cherny (Feb 2026).

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

### Per-feature — Spec & planning (on the main branch)
1.  `/speckit.draftissue`    ← optional; turn an unstructured brief into a well-formed issue
2.  `/speckit.reviewissue`   ← pre-spec gate; posts gaps + recommendations as an issue comment
3.  `/speckit.confirmissue`  ← fold answered review into a `## Confirmed decisions` section
4.  `/speckit.specify`
5.  `/speckit.clarify`       ← iterate until the spec feels complete
6.  `/speckit.checklist`     ← resolve all gaps before continuing
7.  `/speckit.plan`
8.  `/speckit.testplan`      ← review output carefully before continuing
9.  *red-phase commit*       ← commit `test-plan.md` as locked intent (script per project)
10. `/speckit.tasks`
11. `/speckit.analyze`       ← resolve HIGH/CRITICAL before branching; runs the test-plan cross-check via the `after_analyze` hook

### Per-feature — Implementation
12. `/speckit.implement`     ← agent runs to suite-green, keeping the suite green on the inner loop at its own discretion. This is a **soft standard, not a per-turn gate** — the binding "green before a PR" guarantee is the real suite run in step 13 (see *Where the completion gate belongs*).

### Per-feature — Ship
13. `/ship`                  ← one orchestrator: **full suite (the gate) → clean-context review → fix → raise PR.** Runs unattended, so it can drive a loop. Composes what were previously separate manual steps (test-checklist, PR review, slop review, raise-PR).

### Periodic (not per-feature)
- **capture learnings** — fold corrections back into memory/skills. Deliberately *not* part of `/ship`: it needs human curation and batches better across several features, so run it at a supervised checkpoint after a batch.
- **dead-code audit** — every few features or before a release; **not per-PR**.
- **context gardening** — quarterly or after a big architectural shift.

---

## The ship gate

`/ship` exists because the steps between "implementation looks done" and "PR raised" are a fixed sequence with one hard ordering constraint, and a human re-enacting them from prose gets it subtly wrong.

**The suite runs first and everything else is downstream of its exit code.** This ordering is *structural*, not policed: review cannot begin against unverified or red code because it literally runs after the gate. Do not add a hook to enforce the ordering — the exit code **is** the gate. A hook that watches for the agent *claiming* green is exactly the anti-pattern the "gates attach to actions, not prose" rule warns against.

Properties worth copying:

- **Unattended by design.** No prompts anywhere in the flow, so `/ship` can be driven by an automated loop shipping features back-to-back, as well as invoked by hand. Anything that needs a human turns the pipeline into a wait.
- **Stop-on-failure is global.** Suite not green, a reviewer subagent errors, push rejected, PR already exists — stop at that step, report, and run nothing later.
- **Preconditions run before the expensive work.** Check the cheap things first (on a feature branch? any commits over main?), or a full suite and full review burn before a late guard trips.
- **Review runs in clean context.** Reviewers see the diff, not the conversation that produced it. The *deciding and fixing* legitimately happens in the orchestrator's own loop — "review in clean context" governs the reviewing, not the fixing.
- **Validate a finding before acting on it.** Reviewer severities are fickle; cross-check a "Critical" against the actual test and spec state rather than relaying it verbatim. Acting on a mislabelled finding is how a review pass makes code worse.
- **Re-verify what review changed.** Fixes applied after the gate are unverified code — re-run the suite before raising, or a bad fix ships green-unchecked.

**Two reviews, not one.** *Review A* is synchronous and inside `/ship` — clean-context subagents over the diff, whose findings are in-conversation and therefore available to `capture-learnings` later. *Review B* is the asynchronous agent review on the raised PR, for a human to read at merge. `/ship` never waits on Review B: blocking a pipeline for minutes to fold in a second review of the same diff buys little.

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
- **The test-green gate.** The full suite must pass on the code about to become a PR. Put this in the ship flow, not the implement turn — see *Where the completion gate belongs*, below.
- **Traceability gate.** The exact-match half of the test checklist — spec label ↔ test-plan scenario ↔ code marker, character-for-character — is a *deterministic* gate. The judgment-level half (mock self-satisfaction, trivial-pass, fuzzy matches) stays a human-run review command. This one *does* belong at the agent's turn-end, as a loop-guarded nudge rather than a lock-out.
- **No skipped tests.** Skipped / ignored / conditionally-skipped tests (including *runtime* skips) are banned by a static gate — they fake coverage and rot the spec→test trace. Genuinely-untestable scenarios go in a documented "untested branches" table, not a faked skipped test.
- **Fast/slow test categories.** Tag tests *unit* (fast, no external dependencies) vs *integration* (slow, real stack), so the agent gets seconds-fast inner-loop feedback while developing — the whole-suite run remains the completion gate, not the tagged subset.
- **CI on PR** *(where the suite can run in CI).* Build + test on every PR, blocking merge. If the real test stack can't run in hosted CI (heavy infra, private-repo limits), keep the full gate local and let CI cover the deterministic subset only — and say so explicitly.

### Where the completion gate belongs — at ship, not at turn-end

The instinct is to gate the *implement* agent: a turn-end hook that refuses to let it stop until the suite is green. It is the wrong seam, and the reason generalises.

A turn-end hook cannot run the suite itself — a full run is minutes, and the agent's turn is not the place to spend them. So it does the only thing it can: it consults a **ledger** recording that a green run happened at some past moment, and gates on that. But a past green run is a *proxy*. It attests that the suite passed on some earlier state of the code, not that it passes on the code about to become a PR. The plumbing this proxy needs — the ledger, the file markers, the locking, the "was that a whole-suite run or a filtered one?" heuristic — is substantial, and it buys an attestation weaker than the thing you actually wanted.

**Put the gate where the truth is: a real whole-suite run at ship time, immediately before the PR is raised.** It is more machinery removed than added, and the guarantee gets *stronger* — the suite passes **now**, on exactly the diff under review. During implementation, keeping the suite green becomes a soft standard the agent applies at its own discretion on the inner loop, which is where discretion is cheap and a hard gate is merely a tax.

The general rule this instance teaches: **when a gate can only see a proxy for the property you care about, move the gate to where the property itself is observable.** A gate on a ledger is a gate on a claim about the past, which is a short step from the "gates attach to actions, not prose" failure it was meant to avoid.

### How many green runs? One — and fix the flakiness instead

A tempting bar is *N consecutive* green runs, on the reasoning that a non-deterministic stack makes a single green run luck rather than proof. Resist it. **A multi-run bar is a crutch for a flaky suite, and it prices every completion at N× the suite's wall-clock.** The honest reading of "we need three green runs to believe it" is "our suite lies one run in three" — and the fix for that is the flakiness, not the arithmetic.

Set the bar at **one green whole-suite run since the last code change**, and *earn* it: if the suite is genuinely non-deterministic, hunt the flake. Where a multi-run rule already exists, retire it against evidence rather than taste — a provocation campaign (repeated runs under varied seed, concurrency, accumulated state, and CPU pressure) either demonstrates determinism, which retires the rule, or surfaces the flake, which is the thing you actually needed to find.

### Formatting is not verification — do it at ship cadence

Formatting is cosmetic, and cosmetic work does not belong on the inner loop. A format-on-commit hook taxes **every** commit — on a real codebase the tool's workspace load is measured in tens of seconds — to fix something no reviewer would have caught anyway. Fold the format step into the ship flow instead, where it runs once per PR at a cadence that already costs minutes. (Boris Cherny's "formatting handles the last 10%" is right about the value and silent about the cadence; per-commit is the wrong one.)

> Pre-allow safe commands in checked-in settings rather than disabling permission prompts
> wholesale (Boris Cherny): the agent flows, but high-stakes actions still surface.

---

## Context management

Output quality follows a curve (chrismdp): too little context → generic slop; too much → the model drowns in noise and is confidently vague. Two habits:
- **Reset on drift.** Useless refactoring of working code, repeated wrong assumptions, or a "going in circles" feel are signs to start a fresh session — resetting is cheaper than correcting, and models tend to stay lost once they take a wrong turn.
- **Plan mode first for non-trivial work** (Boris Cherny / Karpathy): agree the plan before switching to auto-accept edits. A good plan is often the difference between a one-shot and a spiral. Reserve the heavyweight spec pipeline for features; use lightweight plan-mode for small changes.

For **long-running / multi-session** work (Anthropic): keep a durable progress ledger and a feature list seeded as "all failing," so a later session gets its bearings from git history + progress notes rather than re-deriving state — and can't declare premature victory.

**Prefer the tracker's own state as that ledger.** Before building a progress file, check what the issue tracker already models: issue open/closed, dependency relationships, labels, and PR links are durable, shared, human-visible, and already the thing the team edits. A modern tracker resolves the dependency graph server-side and reports it per issue, so "what is ready to work on now" is a *query*, not a parser and a graph walk you maintain. A bespoke ledger has to be kept true; the tracker is true by construction, and every hand-rolled mirror of it eventually disagrees with it.

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
| Weak success criteria | outcome-level ACs + test plan + the ship gate (a real suite run before the PR) |
| Accidental vibe coding (ship unverified) | the mechanical enforcement layer; the ship gate's structural ordering |
| Review fatigue | move recurring issues into skill files / gates, off the human's plate |
| Harness change locks out the harness | build gates fail-open with an override first, verify, then tighten |

---

## Appendix — Codebase Setup

The kinds of files a project adds to make this workflow operational (names illustrative):

### Agent configuration (`.claude/` or `.agents/`)
- **settings** — checked-in permissions allowlist + hooks: *stale-build guard*, *traceability nudge*, plus any denylist gates backing a standing exclusion; and a deny path over upstream-managed vendored files. Note what is *not* here: the test-green gate is a real suite run inside the ship command, not a hook.
- **commands** — the custom slash commands above (`draftissue`, `reviewissue`, `confirmissue`, `testplan`, `testchecklist`, slop review, dead-code audit, context-gardening, raise-PR, capture-learnings, and the `ship` orchestrator that composes the pre-PR ones).
- **skills / sub-agents** — simplifier, verifier, and any stack-orchestration script.

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

1. **Test every hook standalone with synthetic input** before wiring it into settings.
2. **Build fail-open, with an override, first.** Verify no lock-out, *then* tighten to fail-closed.
3. **One mission per branch, its own PR.** Dogfood the workflow once the gate exists.
4. **A human reviews each hook before it lands in settings.** Non-negotiable.

---

## Token / context management plugins
- **read-once** — avoid re-reading unchanged files.
- **context-mode** — context budgeting.
- **rtk** — token-saving CLI proxy (`rtk gain`).
