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

*Inspired by:*
- [Harness Engineering](https://openai.com/index/harness-engineering/) — OpenAI, 2026.
- [Effective harnesses for long-running agents](https://www.anthropic.com/engineering/effective-harnesses-for-long-running-agents) - Anthropic, 2025.


---

## Workflow Execution Order

### Per-feature — Spec & planning (on main branch)
1.  `/speckit.draftissue`      ← optional; turn an unstructured brief into a well-formed
                                 GitHub issue before review
2.  `/speckit.reviewissue`     ← pre-specification gate; posts gaps + recommendations as an
                                 issue comment. Re-run to expand any question the author
                                 hedged on (`not sure`, `more options`, etc.) — the same
                                 comment is edited in place
3.  `/speckit.confirmissue`    ← fold answered review comment into a `## Confirmed decisions`
                                 section on the issue body, so spec consumes decisions, not
                                 deliberation
4.  `/speckit.specify`         ← prepend scenario naming instruction (see CLAUDE.md)
5.  `/speckit.clarify`         ← iterate until spec feels complete
6.  `/speckit.checklist`       ← resolve all gaps before continuing
7.  `/speckit.plan`
8.  `/speckit.testplan`        ← review output carefully before continuing
9.  `powershell -ExecutionPolicy Bypass -File scripts\git-red-phase-commit.ps1`
10. `/speckit.tasks`
11. `/speckit.analyze`         ← resolve HIGH/CRITICAL before branching; auto-runs
                                 `/speckit.analyze.testplan` via the `after_analyze` hook,
                                 appending a test-plan cross-check to the analyze report

### Per-feature — Implementation
12. `/speckit.implement`       ← agent runs to suite-green

### Per-feature — Pre-PR
13. `/speckit.testchecklist`   ← resolve CRITICAL before continuing
14. `/pr-review-toolkit:review-pr all`      ← re-run dotnet test after simplifier
15. `/review-slop`

### Per-feature — PR
16. `/raise-pr`
17. `/capture-learnings`       ← optional; run after PR is raised

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

**[`.claude/commands/speckit.draftissue.md`](.claude/commands/speckit.draftissue.md)**
Custom `/speckit.draftissue` command. Pre-issue gate that sits *before*
`/speckit.reviewissue`. Takes an unstructured feature brief, grounds it in the codebase,
surfaces the ~5–10 decisions the brief leaves open (with concrete leans), iterates with the
user to lock them, then writes a structured issue body to a transient file and posts it via
`gh issue create`. Output is an issue with substantive scope, acceptance criteria, and an
explicit out-of-scope list — the raw material `/speckit.reviewissue` needs to do useful
cross-checking.

**[`.claude/commands/speckit.reviewissue.md`](.claude/commands/speckit.reviewissue.md)**
Custom `/speckit.reviewissue` command. Pre-specification gate that sits *before*
`/speckit.specify`. Reads an unrefined GitHub issue, cross-references it against the current
codebase (architecture, existing services, test data, docs), and surfaces ambiguities in
scope and undefined semantics (matching rules, thresholds, field lists) that would otherwise
block or distort a specification run. Each gap ends with a concrete `**Recommendation:**`
the author can accept or redirect. The posted comment carries a `<!-- speckit:review -->`
marker; re-runs **edit the same comment in place** to expand any question where the author
hedged (`not sure`, `more options`, `idk`, etc.) — substantive answers are left untouched
for `/speckit.confirmissue` to pick up.

**[`.claude/commands/speckit.confirmissue.md`](.claude/commands/speckit.confirmissue.md)**
Custom `/speckit.confirmissue` command. Sits *between* `/speckit.reviewissue` and
`/speckit.specify`. Reads the answered review comment, pairs each gap's recommendation with
the author's inline answer (accepted / accepted-with-rider / redirected / out-of-scope),
and appends a `## Confirmed decisions` section to the issue body so the spec author
consumes resolved decisions rather than re-reading deliberation. Stops if any answer is
empty or still hedging. The original review comment is left intact as the audit trail.

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

**[`.claudeignore`](.claudeignore)**
Files Claude Code should never read or index (gitignore-style syntax). Excludes build
artefacts (`obj/`), IDE caches (`.vs/`), binary assets (`resources/images/`,
`NetPace.snk`), large test payloads (`src/NetPace.Core.Tests/Payloads/`), archived specs
(`specs/archive/`), and editor/transient files.

### spec-kit configuration (`.specify/`)

**[`.specify/memory/constitution.md`](.specify/memory/constitution.md)**
Project governance: TDD as non-negotiable, library-first architecture, CLI excellence,
cross-platform compatibility, code quality standards, and semantic versioning. Supersedes
all other guides. Versioned; amendments require documented rationale.

**[`.specify/extensions.yml`](.specify/extensions.yml)** +
**[`.specify/extensions/.registry`](.specify/extensions/.registry)**
Spec-kit extension configuration and registry. `extensions.yml` declares lifecycle hooks
(before_*/after_* phases) bound to extension commands; `.registry` tracks installed
extensions with version, priority, and registered commands.

**[`.specify/extensions/git/`](.specify/extensions/git/)**
Git integration extension for spec-kit. Hooks into the spec-kit lifecycle to auto-commit
before and after each spec-kit command (e.g. commit after `/speckit.specify`, commit before
`/speckit.clarify`). Also provides `/speckit.git.feature` to create feature branches and
`/speckit.git.initialize` for project setup. Scripts provided in both Bash and PowerShell.

**[`.specify/extensions/testplan/`](.specify/extensions/testplan/)**
Test-plan cross-check extension. Provides `/speckit.analyze.testplan`, which runs
automatically as an `after_analyze` hook. It cross-checks `test-plan.md` scenarios against
`spec.md` and `tasks.md` for the current feature and appends a *Test Plan Cross-Check*
findings table to the analyze report.

### Spec archive convention (`specs/archive/`)

Once a feature's implementation ships and the branch is merged, its spec folder is moved
from `specs/<NNN-feature-name>/` into `specs/archive/<NNN-feature-name>/`. The folder
structure and files are preserved verbatim — the move is the only change.

- **Why archive rather than delete:** the spec, test plan, tasks, and analyze report are
  the historical record of *what* was built and *why*. `git log` captures the code change;
  the spec folder captures the intent and verification surface behind it.
- **Why a dedicated `archive/` folder:** it keeps `specs/` uncluttered so active features
  are obvious, while preserving the artefacts for future reference.
- **Claude Code exclusion:** `.claudeignore` lists `specs/archive/` so archived specs never
  enter the agent's working context. They remain in the repo and in git history, but the
  agent sees only in-flight work under `specs/`.

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
`~/.claude/read-once/read-once stats`

### context-mode

https://github.com/mksglu/context-mode

`/context-mode:ctx-stats`

### rkt

https://github.com/rtk-ai/rtk

`rtk gain`
