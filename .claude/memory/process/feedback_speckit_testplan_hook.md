---
name: speckit testplan after_analyze hook
description: /speckit.analyze emits EXECUTE_COMMAND for speckit.analyze.testplan but never runs it — read the .md and execute inline before ending the response
type: feedback
---
After `/speckit.analyze` emits `EXECUTE_COMMAND: speckit.analyze.testplan` in the mandatory `## Extension Hooks` block, read `.specify/extensions/testplan/commands/speckit.analyze.testplan.md` and follow its steps inline in the same response. Append its `## Test Plan Cross-Check` findings table to the analyze report.

**Why:** the speckit parent skill has no follow-through for prompt-only extensions (testplan has no bash script). Editing speckit core would break the upstream-upgrade path.

**How to apply:** only this hook. Other speckit hooks are git-backed and self-execute via [speckit hook execution](feedback_speckit_hooks.md).
