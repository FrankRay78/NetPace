# Agentic Software Development Workflow

## Introduction

The following document outlines steps taken to transition to a more 'hands-off' spec driven development (SDD) approach using an agentic software development workflow and practices.

The workflow in one line: write a rigorous spec → generate a test plan → review it → generate tasks → implement feature by feature in parallel worktrees → verify test coverage statically → PR review → merge.


### Design principles

- **Single branch per feature.** Tests and implementation on the same branch. Commit history is the audit trail.
- **test-plan.md is the red-phase baseline.** In a statically-typed C# project, pre-implementation test files cannot compile without the classes they reference. test-plan.md committed on the feature branch serves as the locked intent. Branch protection and the test checklist enforce honesty.
- **PR review as the integrity gate.** Reviewer diffs test-plan.md and checks the testchecklist report, not a binary test pass/fail.
- **Worktrees for parallelism.** Each agent works in its own isolated directory and branch. No coordination required during implementation.


### Documentation Hierarchy

- Tier 1: GOVERNANCE          → .specify/memory/constitution.md (principles)
- Tier 2: IMPLEMENTATION      → .claude/CLAUDE.md (essential patterns + signposts)
- Tier 3: DETAILED REFERENCES → docs/conventions/* (deep guidance)


## Pre-requisites

### Install Spec Driven Development (spec-kit)

```bash
# Install latest cli from main
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git

# Initialize in existing project
specify init . --ai claude
```

https://github.com/github/spec-kit

### Install Claude Code GitHub action for tagging (eg. in pr comments)

"The easiest way to set up this action is through Claude Code in the terminal. Just open claude and run /install-github-app."

https://code.claude.com/docs/en/github-actions


## One Time Setup

### Write the Constitution

Run in Claude Code once per project: `/speckit.constitution`

