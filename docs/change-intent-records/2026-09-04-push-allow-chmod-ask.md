# Allowing `git push`, and moving `chmod` from `deny` to `ask`

**Intent:** Stop `/raise-pr` stalling at its push step, and stop `chmod` being a hard block with no approval path.

**Behaviour:** `.claude/settings.json`; `CONVENTIONS.md` → *Hooks, scripts and plugins* for the `chmod` rule as the `no-chmod` hook's backstop, and gaffer's [`docs/unattended.md`](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/unattended.md) for how an `ask` rule behaves interactively versus headless.

**Decisions:**

- *Rejected — pairing the `git push` allow with a `deny` on `--force`.* Permission rules match on textual prefix, so `Bash(git push --force:*)` would catch `git push --force` and miss `git push -f`, `git push origin main --force`, and every compound form. A guard a flag reorder defeats is worse than none, because it reads as protection. **Force-push is therefore unguarded**, which is a real reduction in cover rather than an oversight: the honest fix is a `PreToolUse` hook that parses the command instead of a prefix rule, and that is worth its own issue.

**Date:** 2026-09-04
