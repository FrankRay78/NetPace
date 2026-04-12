# Agentic Software Development Workflow

## Introduction

The following document outlines steps taken to transition to a more 'hands-off' spec driven
development (SDD) approach using an agentic software development workflow and practices.

**The workflow in one line:** write a rigorous spec → generate a test plan → review it →
generate tasks → implement feature by feature in parallel worktrees → verify test coverage
statically → PR review → merge.


## SDD Workflow Execution Order

### One-time project setup (run once, never again)
1. `/speckit.constitution`

### Per-feature — Spec & planning (on main branch)
2. `/speckit.specify`          ← prepend scenario naming instruction
3. `/speckit.clarify`          ← iterate until spec feels complete
4. `/speckit.checklist`        ← resolve all gaps before continuing
5. `/speckit.plan`
6. `/speckit.testplan`         ← review output carefully before continuing
7. `/speckit.tasks`
8. `/speckit.analyze`          ← resolve HIGH/CRITICAL before branching

### Per-feature — Red-phase (on feature branch)
9.  `git checkout -b <feature-id>`
10. `git add .specify/specs/<feature>/test-plan.md`
11. `git commit -m "test: red phase — test plan for <feature>"`
12. `git push -u origin <feature-id>`

### Per-feature — Implementation (in worktree)
13. `./scripts/setup-worktree.sh <feature-id>`
14. `cd ../<project>-<feature-id> && SPECIFY_FEATURE=<feature-id> claude`
15. `/speckit.implement`        ← agent runs to suite-green

### Per-feature — Pre-PR (back in worktree)
16. `/speckit.testchecklist <feature-id>`   ← resolve CRITICAL before continuing
17. `/pr-review-toolkit:review-pr all`      ← re-run dotnet test after simplifier
18. `/review-slop`

### Per-feature — PR
19. `gh pr create --draft ...`
20. Manual review: diff test-plan.md from red-phase commit
21. Merge (sequential for features sharing files)
22. `git worktree remove ...` + `git branch -d <feature-id>`

### Periodic (not per-feature)
∞  `/audit-deadcode`           ← run every few features or before a release


### Separation of concerns

- **Spec (what & why):** requirements as normative SHALL/MUST statements. No test scenarios.
- **Test plan (how you verify):** named scenarios derived from the spec. Generated after the
  spec is complete, before tasks are decomposed.
- **Tasks (how you build):** implementation breakdown informed by the test plan, so the work
  reflects the full verification surface.
- **Test checklist (did you honour it):** static analysis after implementation confirming
  every scenario has an honest test.


### Design principles

- **Single branch per feature.** Tests and implementation on the same branch. Commit history
  is the audit trail.
- **test-plan.md is the red-phase baseline.** In a statically-typed C# project,
  pre-implementation test files cannot compile without the classes they reference.
  test-plan.md committed on the feature branch serves as the locked intent. Branch protection
  and the test checklist enforce honesty.
- **PR review as the integrity gate.** Reviewer diffs test-plan.md and checks the
  testchecklist report, not a binary test pass/fail.
- **Worktrees for parallelism.** Each agent works in its own isolated directory and branch.
  No coordination required during implementation.


### Documentation hierarchy

- Tier 1: GOVERNANCE          → `.specify/memory/constitution.md` (principles)
- Tier 2: IMPLEMENTATION      → `.claude/CLAUDE.md` (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → `docs/conventions/*` (deep guidance)


### Workflow at a glance

- Phase 0 — One-time project setup
- Phase 1 — Spec, clarification & test plan (per feature)
- Phase 2 — Red-phase commit (per feature)
- Phase 3 — Create worktrees & implement (per feature, in parallel)
- Phase 4 — Test checklist & PR review


---

## Pre-requisites

### Install spec-kit

```bash
# Install latest CLI from main
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git

# Verify
specify check

# Initialize in existing project
specify init . --ai claude
```

Spec-kit reference: <https://github.com/github/spec-kit>

### Install Claude Code GitHub Action

The easiest way is through Claude Code in the terminal — run `/install-github-app`.

Reference: <https://code.claude.com/docs/en/github-actions>

### Install pr-review-toolkit plugin

```
/plugin
```

Search for `pr-review-toolkit` and install. This provides the pre-PR code review step.


---

## Phase 0 — One-Time Project Setup

### 0.1 Install custom slash commands

The following custom commands extend spec-kit. Copy each file into `.claude/commands/`:

| File | Purpose |
|------|---------|
| `speckit.testplan.md` | Generates test-plan.md from spec + plan |
| `speckit.redphase.md` | Commits test-plan.md as the red-phase baseline |
| `speckit.testchecklist.md` | Static analysis: verifies test plan is fully honoured post-implementation |

Also install the analyze override, which extends `/speckit.analyze` to cross-check
test-plan.md against tasks when present:

```
.specify/templates/overrides/analyze.md   ← save from analyze-override.md
```

### 0.2 Write the constitution

Run in Claude Code once per project:

```
/speckit.constitution
```

In addition to the automatically generated constitution (which is normally pretty good), 
the following sections should be added:

**1. Testing (NON-NEGOTIABLE)**
```
No implementation code before a test plan exists for that behaviour.
Do not skip, trivially mock, or special-case tests to make them pass.
```

**2. Implementation loop**
```
- Run the full test suite after each task for signal
- Partial failures mid-branch are expected — do not mock to hide them
- The branch is not complete until the full suite is green
- Do not open a PR until all tests pass
- Commit after each completed task using the HEREDOC format:
    git commit -m "$(cat <<'EOF'
    feat: description of what and why
    EOF
    )"
- After each commit, mark the task [x] in tasks.md and append one line
  to PROGRESS.md: "T### completed — <what was done and any key decisions>"
```

> **Note:** Do not add rules that duplicate what the workflow already enforces. Worktree
> isolation handles scope, `/speckit.implement` handles task ordering, CI enforces
> suite-green, and the test checklist enforces coverage.


#### Add this to CLAUDE.md

```
### Recording Decisions

During implementation, route decisions as follows:

- **Task completion notes** → append one line to
  `.specify/specs/<feature>/progress.md`:
  `"T### — <what was done>"`

- **Architectural decisions** (chose between alternatives, worked around a
  constraint, affects future work) → create a CIR in
  `docs/change-intent-records/` using the template in
  `docs/conventions/change-intent-records.md`

When in doubt: if a future developer or agent might wonder *why* something
was done this way, it's a CIR. If it's just *what* was done, it's a
progress.md note.
```

#### Add this to constitution.md

```
## Testing (NON-NEGOTIABLE)

No implementation code before a test plan exists for that behaviour.
Do not skip, trivially mock, or special-case tests to make them pass.

## Implementation loop

- Run the full test suite after each task for signal
- Partial failures mid-branch are expected — do not mock to hide them
- The branch is not complete until the full suite is green
- Do not open a PR until all tests pass
- Commit after each completed task using the HEREDOC format:
    git commit -m "$(cat <<'EOF'
    feat: description of what and why
    EOF
    )"
- After each commit, mark the task [x] in tasks.md and append one line
  to .specify/specs/$FEATURE/progress.md:
  "T### — <what was done>"
- If a decision involved choosing between viable alternatives, working around
  a constraint, or affects future work: create a CIR in
  docs/change-intent-records/ instead of a progress.md note
```


### 0.3 Create Claude Code hooks

Hooks enforce constitution rules deterministically — the agent cannot bypass them.
All hooks live in `.claude/hooks/` and are registered in `.claude/settings.json`.

```bash
mkdir -p .claude/hooks
```

**Hook 1 — Auto-format on file edit** (`.claude/hooks/dotnet-format.sh`):

```bash
#!/bin/bash
# Runs dotnet format on the file Claude just edited. Only acts on .cs files.
INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
[[ "$FILE_PATH" == *.cs ]] || exit 0
dotnet format NetPace.sln --include "$FILE_PATH" \
  --severity warn --no-restore 2>/dev/null || true
exit 0
```

**Hook 2 — Protect test-plan.md from modification** (`.claude/hooks/protect-testplan.sh`):

```bash
#!/bin/bash
# Blocks any write to test-plan.md during implementation.
# test-plan.md is locked at the red-phase commit.
INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | jq -r '.tool_input.file_path // empty')
if [[ "$FILE_PATH" == *"test-plan.md" ]]; then
  echo "Blocked: test-plan.md is locked after the red-phase commit." >&2
  echo "To update the test plan, open a new spec iteration instead." >&2
  exit 2
fi
exit 0
```

**Hook 3 — Require tests green before session ends** (`.claude/hooks/require-green.sh`):

```bash
#!/bin/bash
# Blocks Claude from declaring itself done if the test suite is failing.
OUTPUT=$(dotnet test --no-build --verbosity quiet 2>&1)
EXIT_CODE=$?
if [ $EXIT_CODE -ne 0 ]; then
  echo "Test suite is not green. Fix failing tests before stopping." >&2
  echo "$OUTPUT" | grep -E "Failed|Error" | head -10 >&2
  exit 2
fi
exit 0
```

Make all hooks executable:

```bash
chmod +x .claude/hooks/dotnet-format.sh
chmod +x .claude/hooks/protect-testplan.sh
chmod +x .claude/hooks/require-green.sh
```

Register them in `.claude/settings.json`:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/dotnet-format.sh"
          }
        ]
      }
    ],
    "PreToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/protect-testplan.sh"
          }
        ]
      }
    ],
    "Stop": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/require-green.sh",
            "timeout": 120
          }
        ]
      }
    ]
  }
}
```

Verify with `/hooks` inside Claude Code — you should see PostToolUse (1), PreToolUse (1),
and Stop (1).

### 0.4 Create the git pre-commit hook

This runs at the git level and catches compile errors at every commit.

Save as `.git/hooks/pre-commit`:

```bash
#!/bin/bash
# Pre-commit: build check only (fast).
# Full test suite is enforced by the Claude Code Stop hook and by CI on PR.
echo "→ Building solution..."
if ! dotnet build NetPace.sln --no-incremental --verbosity quiet 2>&1; then
  echo ""
  echo "❌ Build failed. Fix compilation errors before committing." >&2
  exit 1
fi
echo "✓ Build passed."
exit 0
```

```bash
chmod +x .git/hooks/pre-commit
```

> **Note:** `.git/hooks/` is not committed to the repository. For worktrees, git hooks are
> shared from the main `.git` directory — install once, applies everywhere.

### 0.5 Create the worktree setup script

Save as `scripts/setup-worktree.sh` and make it executable. This works around Claude Code
bug #28041 where worktrees do not receive `.claude/` config by default.

```bash
#!/bin/bash
# Usage: ./scripts/setup-worktree.sh <feature-id>
# Example: ./scripts/setup-worktree.sh 001-auth

set -e
FEATURE=$1
WORKTREE_DIR="../$(basename $(pwd))-${FEATURE}"

[ -z "$FEATURE" ] && echo "Usage: $0 <feature-id>" && exit 1

git worktree add "$WORKTREE_DIR" "${FEATURE}"

# Create .claude/ and copy all config (workaround for bug #28041)
mkdir -p "$WORKTREE_DIR/.claude"
cp -r .claude/rules        "$WORKTREE_DIR/.claude/rules"       2>/dev/null || true
cp    .claude/settings.json "$WORKTREE_DIR/.claude/settings.json" 2>/dev/null || true
cp -r .claude/scripts      "$WORKTREE_DIR/.claude/scripts"     2>/dev/null || true
cp -r .claude/commands     "$WORKTREE_DIR/.claude/commands"    2>/dev/null || true
[ -f .env ] && cp .env "$WORKTREE_DIR/.env"

# Create PROGRESS.md so the agent has a handoff file from the start
cat > "$WORKTREE_DIR/PROGRESS.md" << EOF
# Progress — ${FEATURE}

## Status
In progress

## Completed tasks
<!-- Agent: append one line per completed task: "T### — description and key decisions" -->

## Current task
<!-- Agent: update this when starting each task -->

## Key decisions made
<!-- Agent: note any architectural decisions made during implementation -->

## Blockers / open questions
<!-- Agent: note anything that needs human input -->
EOF

echo "✅ Worktree ready: $WORKTREE_DIR on branch ${FEATURE}"
echo "   Run: cd $WORKTREE_DIR && SPECIFY_FEATURE=${FEATURE} claude"
```

```bash
chmod +x scripts/setup-worktree.sh
```

### 0.6 Configure CI

In GitHub → Settings → Branches, add a branch protection rule for main: require status
checks to pass. Save as `.github/workflows/test.yml`:

```yaml
name: Tests
on: [pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run tests
        run: dotnet test   # or: npm test / pytest
```


---

## Phase 1 — Spec, Clarification & Test Plan

All steps run in the main repo on the main branch. The goal is a complete, reviewed test
plan before any feature branch is created.

### 1.1 Specify

```
/speckit.specify
```

Describe what you want to build — what and why, not the tech stack. The spec template
override keeps output as clean `### Requirement:` blocks with no test noise.

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

"Unit tests for English." Validates requirements are complete, clear, measurable, and
consistent. Resolve any flagged gaps in spec.md before generating the test plan — the test
plan is only as good as the spec it is derived from.

### 1.4 Plan

```
/speckit.plan
```

Provide your tech stack and architecture constraints. Generates plan.md, data-model.md,
research.md, and API contracts. The test plan draws on plan.md for module names and
endpoint context.

### 1.5 Generate test plan

```
/speckit.testplan <feature-id>
```

Think hard before running this. Translates each requirement in spec.md into named WHEN/THEN
test scenarios, co-located with the other spec artifacts at
`.specify/specs/<feature>/test-plan.md`. See `speckit.testplan.md` for full command details.

Review the generated test-plan.md carefully:

- Check requirements flagged as having only one scenario — likely missing failure modes
- Add scenarios for edge cases from `/speckit.clarify` not already captured
- Remove anything describing implementation internals rather than observable behaviour
- If adding a scenario not yet in spec.md, update spec.md first — keep it as the source
  of truth

> **Tip:** The test plan is the bridge between spec and tasks. Review it before generating
> tasks — the implementation decomposition should reflect the full verification surface.

### 1.6 Generate tasks

```
/speckit.tasks
```

Breaks the plan into atomic, ordered, parallelisable tasks. Tasks marked `[P]` can run in
parallel. Because test-plan.md now exists in the spec directory, the agent decomposes work
with awareness of what needs to be verified, not just what needs to be built.

### 1.7 Analyse

```
/speckit.analyze
```

Cross-checks spec, plan, tasks — and test-plan.md if present (via the override) — for
contradictions and coverage gaps. Resolve any HIGH or CRITICAL findings before creating
feature branches. Note which features touch the same files: they will need sequential
merging in Phase 4.


---

## Phase 2 — Red-Phase Commit

In a statically-typed C# project, test files cannot compile before the classes they
reference exist. test-plan.md therefore serves as the red-phase baseline — the locked
verification intent committed before implementation begins.

### 2.1 Create the feature branch

```bash
git checkout -b 001-auth
```

test-plan.md was generated on main in Phase 1. Creating the branch from main carries it
forward automatically.

### 2.2 Commit test-plan.md as the red-phase baseline

```bash
git add .specify/specs/001-auth/test-plan.md
git commit -m "test: red phase — test plan for 001-auth"
git push -u origin 001-auth
```

> **Tip:** Include the commit SHA in the PR description when you open it. Reviewers use it
> to confirm test-plan.md was not modified after this point:
> `git diff <sha> HEAD -- .specify/specs/001-auth/test-plan.md`


---

## Phase 3 — Worktrees & Parallel Implementation

### 3.1 Create worktrees

Run the setup script for each feature branch:

```bash
./scripts/setup-worktree.sh 001-auth
./scripts/setup-worktree.sh 002-payments
./scripts/setup-worktree.sh 003-search
```

Each worktree is created as a sibling directory checked out on its feature branch, with a
full copy of `.claude/` config and a pre-populated PROGRESS.md.

Verify:

```bash
git worktree list
```

### 3.2 Launch one agent per feature

Open one terminal per feature and start Claude Code in the worktree:

```bash
cd ../my-project-001-auth && SPECIFY_FEATURE=001-auth claude
cd ../my-project-002-payments && SPECIFY_FEATURE=002-payments claude
```

`SPECIFY_FEATURE` overrides spec-kit feature detection if it does not identify the correct
spec automatically from the branch name.

### 3.3 Implement

```
/speckit.implement
```

Each agent reads the spec, plan, and tasks for its feature, works through tasks in order,
runs the full test suite after each task, commits after each completed task, and updates
PROGRESS.md. The Claude Code Stop hook prevents the agent from declaring completion until
the suite is green.

Partial failures during implementation are expected — the contract is full suite green at
the end of all tasks, not after each individual task.

**Restartability:** If a session is interrupted or context is compacted, the next session
reads tasks.md (which shows completed `[x]` tasks) and PROGRESS.md to re-orient without
re-reading the entire codebase.

> **Note:** If the agent stops because tests are failing mid-implementation, add a note to
> that feature's tasks.md:
> ```
> NOTE: Many tests will remain failing until later tasks are complete.
> Continue through all tasks. Suite-green at the end is the contract.
> ```

### 3.4 Monitor progress

```bash
git -C ../my-project-001-auth log --oneline -8
cat ../my-project-001-auth/PROGRESS.md
```


---

## Phase 4 — Test Checklist & PR Review

### 4.1 Run the test checklist

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
See `speckit.testchecklist.md` for full details.

### 4.2 Run code review

```
/pr-review-toolkit:review-pr all
```

Runs specialist agents against the diff: general code reviewer (CLAUDE.md compliance,
bugs), silent failure hunter (async error handling patterns), comment analyzer, and code
simplifier. The simplifier modifies files — re-run `dotnet test` after it completes to
confirm the suite is still green.

### 4.3 Run the slop reviewer

```
/review-slop
```

Diffs the branch against main and flags AI-generated code patterns that compile and pass
lint but degrade the codebase: over-comments, defensive try/catch around code that can't
fail, unnecessary abstractions, type workarounds. Produces a cleaned diff.

This catches a category of issues that the pr-review-toolkit misses because the code
technically works.

### 4.4 Open pull request

```bash
gh pr create \
  --draft \
  --base main \
  --title "feat: 001-auth" \
  --body "$(cat <<'EOF'
## Summary
- [Brief description of what this PR does and why]

## Test plan
- [ ] All scenarios in test-plan.md have a corresponding test
- [ ] /speckit.testchecklist passed with no CRITICAL findings
- [ ] /pr-review-toolkit:review-pr all completed, findings addressed
- [ ] dotnet test green after code simplification
- [ ] No test-plan.md changes after red-phase commit: git diff <red-phase-sha> HEAD -- .specify/specs/<feature>/test-plan.md

## Red-phase baseline
Commit SHA: <red-phase-commit-sha>
EOF
)"
```

### 4.5 PR review checklist

**Test plan integrity**

Confirm test-plan.md has not changed since the red-phase commit:

```bash
git diff <red-phase-sha> HEAD -- .specify/specs/001-auth/test-plan.md
```

Any change to test-plan.md after the red-phase commit requires explanation — was a scenario
added for a legitimate discovered requirement, or was a scenario removed to avoid
implementing something?

**Implementation quality**

- All tests pass — enforced by CI, PR cannot merge without this
- No cheating: look for `[Fact(Skip=...)]`, `[Ignore]`, `Assert.True(true)`, hardcoded
  return values, or mocks asserted against directly
- Implementation satisfies the spec's intent, not just the literal test assertions
- Code follows the constitution

### 4.6 Merge order

Features with no shared files merge in any order. For features `/speckit.analyze` flagged
as sharing files, merge in dependency order and rebase:

```bash
git fetch origin && git rebase origin/main
```

Re-run the full suite after rebase. Open the PR when green.

### 4.7 Clean up

```bash
git worktree remove ../my-project-001-auth
git branch -d 001-auth
```


---

## Periodic maintenance

### Dead code audit

Run after several features have merged, or before a release:

```
/audit-deadcode
```

Starts from CLI entry points, walks the full call graph, cross-checks against config files,
and produces a `DEADCODE.md` report. Agents are bad at cleaning up after themselves — this
catches refactored functions whose old versions were never removed.


---

## Quick Reference

### Command sequence per feature

| Stage | Commands |
|-------|---------|
| Spec | `/speckit.specify` → `/speckit.clarify` → `/speckit.checklist` → `/speckit.plan` |
| Test plan | `/speckit.testplan <id>` → review test-plan.md |
| Tasks | `/speckit.tasks` → `/speckit.analyze` |
| Red phase | `git checkout -b <id>` → commit test-plan.md → push |
| Implement | `setup-worktree.sh` → `SPECIFY_FEATURE=<id> claude` → `/speckit.implement` |
| Pre-PR | `/speckit.testchecklist <id>` → `/pr-review-toolkit:review-pr all` → `/review-slop` |
| PR | diff test-plan.md from red-phase commit → CI gate → merge |

### Slash commands

**Built-in (spec-kit)**

| Command | Purpose |
|---------|---------|
| `/speckit.constitution` | Project rules (once per project) |
| `/speckit.specify` | Write requirements |
| `/speckit.clarify` | Surface edge cases |
| `/speckit.checklist` | Validate requirement quality |
| `/speckit.plan` | Technical architecture |
| `/speckit.tasks` | Implementation task breakdown |
| `/speckit.analyze` | Cross-artifact consistency (extended by override) |
| `/speckit.implement` | Execute tasks (run per worktree) |

**Custom (install to `.claude/commands/`)**

| Command | Purpose |
|---------|---------|
| `/speckit.testplan` | Generate test-plan.md from spec + plan |
| `/speckit.testchecklist` | Static analysis: verify test plan is fully honoured |
| `/review-slop` | Flag AI-generated code that compiles but degrades the codebase |
| `/audit-deadcode` | Find unused code from entry points (run periodically) |

**Plugins**

| Command | Purpose |
|---------|---------|
| `/pr-review-toolkit:review-pr all` | Full pre-PR review: bugs, silent failures, simplification |

### Key files

```
.specify/memory/constitution.md                   — project rules
.specify/templates/overrides/spec-template.md     — requirements-only spec format
.specify/templates/overrides/analyze.md           — analyze override with test-plan.md pass
.specify/specs/<feature>/spec.md                  — requirements (SHALL/MUST)
.specify/specs/<feature>/plan.md                  — technical plan
.specify/specs/<feature>/test-plan.md             — test scenarios (spec artifact, not test code)
.specify/specs/<feature>/tasks.md                 — implementation tasks (tracks [x] completion)
.claude/settings.json                             — hooks configuration
.claude/hooks/dotnet-format.sh                    — auto-format on file edit
.claude/hooks/protect-testplan.sh                 — block test-plan.md modification
.claude/hooks/require-green.sh                    — require suite green before Stop
.claude/commands/speckit.testplan.md              — testplan command prompt
.claude/commands/speckit.redphase.md              — redphase command prompt
.claude/commands/speckit.testchecklist.md         — testchecklist command prompt
.claude/commands/review-slop.md                   — AI slop reviewer command
.claude/commands/audit-deadcode.md                — dead code audit command
scripts/setup-worktree.sh                         — worktree creation + PROGRESS.md init
.git/hooks/pre-commit                             — build check on every commit
.github/workflows/test.yml                        — CI: suite green required on PR
PROGRESS.md                                       — per-worktree handoff and progress log
```
