# Agentic Software Development Workflow

## Introduction

Writing a spec before touching code, locking a test plan before writing a test, and
enforcing both mechanically — that is the discipline this workflow encodes. The result is a
harness: Claude Code constrained by context, feedback loops, and automated quality gates so
that the agent does the work and the engineer reviews it.

**The workflow in one line:** write a rigorous spec → generate a test plan → review it →
generate tasks → implement → verify test coverage → PR review → merge.

The one-time project setup is already done. See the [Appendix](#appendix--codebase-setup)
for the files that make the workflow run.

*Inspired by [Harness Engineering](https://openai.com/index/harness-engineering/) — OpenAI, 2026.*


---

## Workflow Execution Order

### Per-feature — Spec & planning (on main branch)
1. `/speckit.specify`          ← prepend scenario naming instruction (see CLAUDE.md)
2. `/speckit.clarify`          ← iterate until spec feels complete
3. `/speckit.checklist`        ← resolve all gaps before continuing
4. `/speckit.plan`
5. `/speckit.testplan`         ← review output carefully before continuing
6. `/speckit.tasks`
7. `/speckit.analyze`          ← resolve HIGH/CRITICAL before branching

### Per-feature — Red-phase (on feature branch)
8.  `git checkout -b <feature-id>`
9.  `git add .specify/specs/<feature>/test-plan.md`
10. `git commit -m "test: red phase — test plan for <feature>"`
11. `git push -u origin <feature-id>`

`powershell -ExecutionPolicy Bypass -File scripts\git-red-phase-commit.ps1`



### Per-feature — Implementation
12. `/speckit.implement`       ← agent runs to suite-green

### Per-feature — Pre-PR
13. `/speckit.testchecklist <feature-id>`   ← resolve CRITICAL before continuing
14. `/pr-review-toolkit:review-pr all`      ← re-run dotnet test after simplifier
15. `/review-slop`

### Per-feature — PR
16. `/raise-pr`
17. Manual review: diff test-plan.md from red-phase commit
18. Merge (sequential for features sharing files)

### Periodic (not per-feature)
∞  `/audit-deadcode`           ← run every few features or before a release


---

## Separation of Concerns

- **Spec (what & why):** requirements as normative SHALL/MUST statements. No test scenarios.
- **Test plan (how you verify):** named scenarios derived from the spec. Generated after the
  spec is complete, before tasks are decomposed.
- **Tasks (how you build):** implementation breakdown informed by the test plan, so the work
  reflects the full verification surface.
- **Test checklist (did you honour it):** static analysis after implementation confirming
  every scenario has an honest test.


## Design Principles

- **Single branch per feature.** Tests and implementation on the same branch. Commit history
  is the audit trail.
- **test-plan.md is the red-phase baseline.** In a statically-typed C# project,
  pre-implementation test files cannot compile without the classes they reference.
  test-plan.md committed on the feature branch serves as the locked intent. The test
  checklist enforces honesty.
- **PR review as the integrity gate.** Reviewer diffs test-plan.md and checks the
  testchecklist report, not a binary test pass/fail.


## Documentation Hierarchy

- Tier 1: GOVERNANCE          → `.specify/memory/constitution.md` (principles)
- Tier 2: IMPLEMENTATION      → `.claude/CLAUDE.md` (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → `docs/conventions/*` (deep guidance)


---

## Phase 1 — Spec, Clarification & Test Plan

All steps run on the main branch. The goal is a complete, reviewed test plan before any
feature branch is created.

### 1.1 Specify

```
/speckit.specify
```

Describe what you want to build — what and why, not the tech stack.

**Important:** prepend the following instruction before your feature description so
acceptance scenarios have named labels, which are required for `/speckit.testchecklist`
traceability:

```
Each acceptance scenario must have a descriptive name label on its own line,
formatted as: **Scenario: [Descriptive name]**
followed by: Given [state], When [action], Then [outcome]

Include at least one failure/error scenario and one boundary scenario per user
story — not just the happy path.
```

### 1.2 Clarify

```
/speckit.clarify
```

Spec-kit interviews you about gaps and edge cases. Run iteratively until the spec feels
complete. Reflect any edge cases discovered here back into spec.md as requirements before
moving on — they will become test scenarios.

### 1.3 Checklist

```
/speckit.checklist
```

Validates requirements are complete, clear, measurable, and consistent. Resolve any flagged
gaps in spec.md before generating the test plan.

### 1.4 Plan

```
/speckit.plan
```

Provide your tech stack and architecture constraints. Generates plan.md, data-model.md,
research.md, and API contracts. The test plan draws on plan.md for module names and
endpoint context.

### 1.5 Generate Test Plan

```
/speckit.testplan <feature-id>
```

Translates each requirement in spec.md into named WHEN/THEN test scenarios, written to
`.specify/specs/<feature>/test-plan.md`.

Review the generated test-plan.md carefully:

- Check requirements flagged as having only one scenario — likely missing failure modes
- Add scenarios for edge cases from `/speckit.clarify` not already captured
- Remove anything describing implementation internals rather than observable behaviour
- If adding a scenario not yet in spec.md, update spec.md first

### 1.6 Generate Tasks

```
/speckit.tasks
```

Breaks the plan into atomic, ordered tasks. Because test-plan.md exists, the agent
decomposes work with awareness of what needs to be verified, not just what needs to be
built.

### 1.7 Analyse

```
/speckit.analyze
```

Cross-checks spec, plan, tasks, and test-plan.md for contradictions and coverage gaps.
Resolve any HIGH or CRITICAL findings before creating feature branches. Note which features
touch the same files — they will need sequential merging.


---

## Phase 2 — Red-Phase Commit

test-plan.md serves as the red-phase baseline — the locked verification intent committed
before implementation begins. In a statically-typed C# project, test files cannot compile
before the classes they reference exist, so test-plan.md is the proxy for the red phase.

### 2.1 Create the feature branch

```bash
git checkout -b <feature-id>
```

test-plan.md was generated on main in Phase 1. Creating the branch from main carries it
forward automatically.

### 2.2 Commit test-plan.md as the red-phase baseline

```bash
git add .specify/specs/<feature-id>/test-plan.md
git commit -m "test: red phase — test plan for <feature-id>"
git push -u origin <feature-id>
```

> **Tip:** Include the commit SHA in the PR description when you open it. Reviewers use it
> to confirm test-plan.md was not modified after this point:
> `git diff <sha> HEAD -- .specify/specs/<feature-id>/test-plan.md`


---

## Phase 3 — Implementation

### 3.1 Implement

On the feature branch, start Claude Code and run:

```
/speckit.implement
```

The agent reads the spec, plan, and tasks for the feature, works through tasks in order,
runs the full test suite after each task, and commits after each completed task. The
build-and-test hook (see Appendix) prevents a PR from being opened until the suite is
green.

Partial failures during implementation are expected — the contract is full suite green at
the end of all tasks, not after each individual task.

> **If the agent stops because tests are failing mid-implementation**, add a note to
> tasks.md:
> ```
> NOTE: Many tests will remain failing until later tasks are complete.
> Continue through all tasks. Suite-green at the end is the contract.
> ```

**Restartability:** If a session is interrupted or context is compacted, the next session
reads tasks.md (which shows completed `[x]` tasks) to re-orient without re-reading the
entire codebase.

**Parallel features:** Features that don't share files can be implemented concurrently in
separate git worktrees, each with its own Claude Code session. Run:

```bash
git worktree add ../<project>-<feature-id> <feature-id>
cd ../<project>-<feature-id> && SPECIFY_FEATURE=<feature-id> claude
```


---

## Phase 4 — Test Checklist & PR Review

### 4.1 Run the Test Checklist

Once implementation is complete and all tests pass locally:

```
/speckit.testchecklist <feature-id>
```

Static analysis only — no tests are executed. Reads test-plan.md and the test source files
and verifies:

- Every scenario in test-plan.md has a corresponding test with a `// SCENARIO:` comment
- No test is skipped, trivially passing, or asserting against a mock rather than real
  implementation
- No undocumented tests were added without a corresponding scenario

Resolve all CRITICAL findings before opening a PR. HIGH findings require a judgement call.

### 4.2 Run Code Review

```
/pr-review-toolkit:review-pr all
```

Runs specialist agents against the diff: general code reviewer (CLAUDE.md compliance,
bugs), silent failure hunter (async error handling patterns), comment analyzer, and code
simplifier. The simplifier modifies files — re-run `dotnet test` after it completes to
confirm the suite is still green.

### 4.3 Run the Slop Reviewer

```
/review-slop
```

Diffs the branch against main and flags AI-generated code patterns that compile and pass
lint but degrade the codebase: over-comments, defensive try/catch around code that can't
fail, unnecessary abstractions. Produces a cleaned diff.

### 4.4 Open Pull Request

```
/raise-pr
```

Auto-detects the branch name, infers a PR title, pre-fills the PR body (summary, spec
link, changed files, new artifacts) from the commit history and file diff, then runs
`gh pr create`.

The CI hook in `.claude/settings.json` runs `dotnet build` and `dotnet test` before the
`gh pr create` command executes.

### 4.5 PR Review Checklist

**Test plan integrity**

Confirm test-plan.md has not changed since the red-phase commit:

```bash
git diff <red-phase-sha> HEAD -- .specify/specs/<feature-id>/test-plan.md
```

Any change after the red-phase commit requires explanation.

**Implementation quality**

- All tests pass — enforced by CI, PR cannot merge without this
- No cheating: look for `[Fact(Skip=...)]`, `Assert.True(true)`, hardcoded return values,
  or mocks asserted against directly
- Implementation satisfies the spec's intent, not just the literal test assertions

### 4.6 Merge Order

Features with no shared files merge in any order. For features `/speckit.analyze` flagged
as sharing files, merge in dependency order and rebase:

```bash
git fetch origin && git rebase origin/main
```

Re-run the full suite after rebase. Open the PR when green.

### 4.7 Clean Up

```bash
git worktree remove ../<project>-<feature-id>   # if worktree was used
git branch -d <feature-id>
```


---

## Periodic Maintenance

### Dead Code Audit

Run after several features have merged, or before a release:

```
/audit-deadcode
```

Starts from CLI entry points, walks the full call graph, and produces a `DEADCODE.md`
report. Agents are bad at cleaning up after themselves — this catches refactored functions
whose old versions were never removed.


---

## Quick Reference

### Command Sequence per Feature

| Stage | Commands |
|-------|---------|
| Spec | `/speckit.specify` → `/speckit.clarify` → `/speckit.checklist` → `/speckit.plan` |
| Test plan | `/speckit.testplan <id>` → review test-plan.md |
| Tasks | `/speckit.tasks` → `/speckit.analyze` |
| Red phase | `git checkout -b <id>` → commit test-plan.md → push |
| Implement | `SPECIFY_FEATURE=<id> claude` → `/speckit.implement` |
| Pre-PR | `/speckit.testchecklist <id>` → `/pr-review-toolkit:review-pr all` → `/review-slop` |
| PR | `/raise-pr` → diff test-plan.md from red-phase commit → CI gate → merge |

### Slash Commands

**Built-in (spec-kit)**

| Command | Purpose |
|---------|---------|
| `/speckit.constitution` | Project rules (once per project) |
| `/speckit.specify` | Write requirements |
| `/speckit.clarify` | Surface edge cases |
| `/speckit.checklist` | Validate requirement quality |
| `/speckit.plan` | Technical architecture |
| `/speckit.tasks` | Implementation task breakdown |
| `/speckit.analyze` | Cross-artifact consistency check |
| `/speckit.implement` | Execute tasks (run per feature branch) |

**Custom (installed to `.claude/commands/`)**

| Command | Purpose |
|---------|---------|
| `/speckit.testplan` | Generate test-plan.md from spec + plan |
| `/speckit.testchecklist` | Static analysis: verify test plan is fully honoured |
| `/review-slop` | Flag AI-generated code that compiles but degrades the codebase |
| `/audit-deadcode` | Find unused code from entry points (run periodically) |
| `/raise-pr` | Open a PR with auto-detected spec links and pre-filled body |

**Plugins**

| Command | Purpose |
|---------|---------|
| `/pr-review-toolkit:review-pr all` | Full pre-PR review: bugs, silent failures, simplification |


---

## Appendix — Codebase Setup

The following files were added to this repo to make the SDD workflow operational. They are
listed here as a reference — the setup is already complete.

### Claude Code configuration (`.claude/`)

**[`.claude/settings.json`](.claude/settings.json)**
Permissions allowlist/denylist and two inline hooks:
- *Format on commit* — runs `dotnet format style` and `dotnet format whitespace` on staged
  `.cs` files before any `git commit` Claude Code issues
- *Build and test before PR* — runs `dotnet build` + `dotnet test` before any
  `gh pr create` command executes, blocking PR creation if either fails
- Enables the `pr-review-toolkit` plugin

**[`.claude/commands/speckit.testplan.md`](.claude/commands/speckit.testplan.md)**
Custom `/speckit.testplan` command. Translates completed spec.md requirements into named
WHEN/THEN test scenarios and writes them to `.specify/specs/<feature>/test-plan.md`.
Includes pre-generation quality checks and a post-generation coverage summary table.

**[`.claude/commands/speckit.testchecklist.md`](.claude/commands/speckit.testchecklist.md)**
Custom `/speckit.testchecklist` command. Static analysis (no test execution) that verifies
every scenario in test-plan.md has an honest, non-trivially-passing test with a matching
`// SCENARIO:` comment. Outputs a structured report with CRITICAL / HIGH / WARNING findings.

**[`.claude/commands/review-slop.md`](.claude/commands/review-slop.md)**
Custom `/review-slop` command. Diffs the current branch against main and flags AI-generated
code patterns that compile and pass lint but degrade the codebase.

**[`.claude/commands/audit-deadcode.md`](.claude/commands/audit-deadcode.md)**
Custom `/audit-deadcode` command. Walks the call graph from CLI entry points and produces
a `DEADCODE.md` report of unused code.

**[`.claude/commands/raise-pr.md`](.claude/commands/raise-pr.md)**
Custom `/raise-pr` command. Reads the branch name, commit history, and changed files to
auto-generate a PR title and body (using `.github/pull_request_template.md`), then runs
`gh pr create`.

### spec-kit configuration (`.specify/`)

**[`.specify/memory/constitution.md`](.specify/memory/constitution.md)**
Project governance: TDD as non-negotiable, library-first architecture, CLI excellence,
cross-platform compatibility, code quality standards, and semantic versioning. Supersedes
all other guides. Versioned; amendments require documented rationale.

**[`.specify/extensions.yml`](.specify/extensions.yml)** +
**[`.specify/extensions/git/`](.specify/extensions/git/)**
Git integration extension for spec-kit. Hooks into the spec-kit lifecycle to auto-commit
before and after each spec-kit command (e.g. commit after `/speckit.specify`, commit before
`/speckit.clarify`). Also provides `/speckit.git.feature` to create feature branches and
`/speckit.git.initialize` for project setup. Scripts provided in both Bash and PowerShell.

### GitHub integration (`.github/`)

**[`.github/workflows/dotnet.yml`](.github/workflows/dotnet.yml)**
CI: runs `dotnet build` and `dotnet test` on every PR targeting `main`. PRs cannot merge
without this passing.

**[`.github/workflows/claude.yml`](.github/workflows/claude.yml)**
Claude Code GitHub Action. Triggers on:
- PR opened **by `claude-code[bot]`** — automatically runs a PR review
- `@claude` mentioned in any PR/issue comment, review, or issue body — responds inline

**[`.github/pull_request_template.md`](.github/pull_request_template.md)**
Default PR body template with sections for: Summary, Spec (link to
`.specify/specs/<feature>/`), Changed Files, and New Artifacts (spec folders and CIR
files). Pre-filled automatically by `/raise-pr`.

### Conventions

**[`docs/conventions/csharp-style.md`](docs/conventions/csharp-style.md)**
Detailed C# style reference: field naming, file-scoped namespaces, ConfigureAwait patterns,
collection expressions, brace style, member ordering.

**[`docs/conventions/change-intent-records.md`](docs/conventions/change-intent-records.md)**
When and how to write a Change Intent Record (CIR) — for decisions involving viable
alternatives, constraint workarounds, or anything that affects future work.
CIRs live in `docs/change-intent-records/`.
