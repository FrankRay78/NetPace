# Removing `rm`/`rmdir` from `permissions.ask`

**Intent:** Let `/verify`'s clean-context review run to completion without a human. Reviewer subagents stand up synthetic fixtures to exercise failure paths and tear them down again, so teardown hits `rm` on nearly every run — and an `ask` rule is never auto-approved in any mode, `bypassPermissions` included, so removing the rule was the only lever that reaches the behaviour.

**Behaviour:** `.claude/settings.json`; `docs/agentic-workflow-NetPace.md` → *Permissions*.

**Decisions:**

- *Rejected — a scratch-scoped `PreToolUse` hook.* Keep `Bash(rm:*)` on `ask` and auto-approve only removals confined to `.claude/scratch/` and the session scratchpad, so `rm -rf src/` still prompts. This preserves the most protection, and it was the closer call. It lost on proportion: a hook script plus its `.tests.sh` matrix and a `hooks/README.md` entry is roughly five times the size of the change, to guard a tree that is fully recoverable from git in a box that is itself disposable. **When the guard outgrows the fix, the mechanism is wrong.**

**Date:** 2026-09-04
