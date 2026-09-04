# Removing `rm`/`rmdir` from `permissions.ask`

**Intent:** Let `/ship` step 2 run to completion without a human, which is what its own documentation promises. Reviewer subagents stand up synthetic fixtures to exercise failure paths and tear them down again, so teardown hits `rm` on nearly every run — and every one of those stopped the run.

**Behaviour:**
- Given a session started in `bypassPermissions`, when `/ship` step 2 spawns the `pr-review-toolkit` reviewers, then they build, exercise and tear down a synthetic `PATH` / `NETPACE_CLAUDE_HOME` fixture with no permission prompt.
- Given the same workflow run headless (`claude -p --dangerously-skip-permissions`), when a reviewer removes its fixture, then the removal succeeds rather than being silently denied.
- Given `rm -rf` aimed at a critical path (`.git`, `.claude`, a dotfile), when any mode is active, then Claude Code still refuses it.

**Constraints:**
- `ask` rules are never auto-approved in any mode, `bypassPermissions` included, and `ask` outranks `allow` — so no allowlist entry and no `PreToolUse` hook could exempt the reviewers' teardown while the rule stood. Removing the rule was the only lever that reaches the behaviour.
- NetPace runs in a disposable, sandboxed cloud Linux box with all work in git and pushed. A destructive `rm` inside the working tree is recoverable; the confirmation step was buying a warning, not a safety net.
- The built-in critical-path guard is not ours to weaken and does not depend on this rule.

**Decisions:**

*Rejected — a scratch-scoped `PreToolUse` hook.* Keep `Bash(rm:*)` in `ask` and auto-approve only removals confined to `.claude/scratch/` and the session scratchpad, so `rm -rf src/` still prompts. This preserves the most protection, and it was the closer call. It lost on proportion: a hook script plus its `.tests.sh` matrix and a `hooks/README.md` entry is roughly five times the change, to guard a tree that is fully recoverable from git in a box that is itself disposable. When the guard outgrows the fix, the mechanism is wrong.

*Rejected — clearing the whole `ask` list.* The same "no point approving manually in a sandbox" reasoning does retire most of the remaining entries, but the `git` rules reach outside the sandbox, and `Bash(git push:*)` is already owned by a separate open issue. Folding that in would have put two missions on one branch.

*Rejected — narrowing the rule, e.g. `Bash(rm -rf /:*)`.* Matching is textual and the reviewers' teardown is `rm -rf "$SB"`, expanded at runtime, so a narrower literal rule would miss the dangerous case as readily as the benign one. A guard that a variable defeats is worse than no guard, because it reads as protection.

*Note on the diagnosis.* The originating issue inferred that subagents were not inheriting the parent's `bypassPermissions` and falling back to `defaultMode`. That is not what happens — inheritance works and cannot be overridden — and the evidence for it (no `permissionMode` records for the reviewers) was an artifact: subagent turns are not written to the project transcript at all. The mechanism, and the headless-as-oracle technique that settled it, are recorded in [`../agentic-workflow-NetPace.md`](../agentic-workflow-NetPace.md#permissions-and-unattended-runs).

**Date:** 2026-09-04
