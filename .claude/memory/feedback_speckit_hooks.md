---
name: speckit hook execution
description: How to correctly execute speckit git hooks — bash script directly, never via Skill tool
type: feedback
---
All speckit skills have `disable-model-invocation: true`. They are user-invocable only (user types `/speckit.specify`, etc). NEVER call them via the Skill tool.

**Why:** The Skill tool invocation is blocked by `disable-model-invocation: true`. The skills load as prompts injected by the Claude Code harness when the user types the slash command — they are not callable by the model.

**How to apply:** When a mandatory hook fires (e.g., `EXECUTE_COMMAND: speckit.git.commit` with event `after_specify`), run the underlying bash script directly:

```bash
.specify/extensions/git/scripts/bash/auto-commit.sh <event_name>
```

Replace `<event_name>` with the hook event (e.g., `after_specify`, `after_clarify`, `after_plan`).

The auto-commit script reads `.specify/extensions/git/git-config.yml` to decide whether to actually commit. If the event is not enabled there, it exits silently.

Skill name mapping (dots → hyphens, for reference only):
- `speckit.git.commit` → `.specify/extensions/git/scripts/bash/auto-commit.sh`
- `speckit.git.feature` → `.specify/extensions/git/scripts/bash/create-new-feature.sh`
