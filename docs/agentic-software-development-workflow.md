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
6. `powershell -ExecutionPolicy Bypass -File scripts\git-red-phase-commit.ps1`
7. `/speckit.tasks`
8. `/speckit.analyze`          ← resolve HIGH/CRITICAL before branching

### Per-feature — Implementation
12. `/speckit.implement`       ← agent runs to suite-green

### Per-feature — Pre-PR
13. `/speckit.testchecklist`   ← resolve CRITICAL before continuing
14. `/pr-review-toolkit:review-pr all`      ← re-run dotnet test after simplifier
15. `/review-slop`

### Per-feature — PR
16. `/raise-pr`
17. `/capture-learnings`    ← optional; run after PR is raised

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
- Tier 2: IMPLEMENTATION      → `CLAUDE.md` (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → `docs/conventions/*` (deep guidance)


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

**[`.claude/commands/capture-learnings.md`](.claude/commands/capture-learnings.md)**
Custom `/capture-learnings` command. Scans the branch's commit messages and diff for
signals of corrections, decisions, and gotchas, then surfaces candidates for the user to
approve and persist as memory entries.

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


---

## Claude Token Management Plugins

### read-once

https://github.com/Bande-a-Bonnot/Boucle-framework/blob/main/tools/read-once/README.md

`C:\Users\frank\.claude\read-once\read-once.ps1 stats`

### context-mode

https://github.com/mksglu/context-mode

`/context-mode:ctx-stats`

### rkt

https://github.com/rtk-ai/rtk

`rtk gain`