---
name: Run speckit git hooks via their bash script
description: speckit.* skills are user-only (disable-model-invocation); when a hook emits EXECUTE_COMMAND speckit.git.commit, run the script directly.
type: feedback
---

When a speckit hook emits `EXECUTE_COMMAND: speckit.git.commit` (event `after_specify`, `after_plan`, …), run `.specify/extensions/git/scripts/bash/auto-commit.sh <event>`. Don't use the Skill tool.

**Why:** Every speckit skill sets `disable-model-invocation: true`, so the Skill tool refuses it.

**How to apply:** The script checks `.specify/extensions/git/git-config.yml`, where all events are currently disabled, so a silent exit is expected, not an error.
