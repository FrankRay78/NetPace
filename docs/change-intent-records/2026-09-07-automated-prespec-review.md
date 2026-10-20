# Running the Pre-Specification Review on Labelled Issues

**Intent:** Take the one step in the issue gate whose cost is the *wait* — `/reviewissue`, which carries the codebase-grounding pass — off the developer machine, so an issue raised away from the desk has its gap analysis waiting rather than requested.

**Behaviour:** [`.github/workflows/reviewissue.yml`](../../.github/workflows/reviewissue.yml), and `docs/agentic-workflow-NetPace.md` → *The whole pre-build issue gate runs in CI*.

**Decisions:**

- **`timeout-minutes: 30`, and no `--max-turns`.** Standard runners are free on a public repository and turns are not metered per unit on this plan, so only a hung job is worth guarding. `--max-turns` is the one limit that could silently shorten the codebase-grounding pass and quietly degrade the review into generic advice, which is the exact failure this automation exists to avoid.

**Known residuals:**

- **The tool allowlist narrows nothing.** `--allowedTools` *adds* to the permission rules Claude Code loads from the checked-out [`.claude/settings.json`](../../.claude/settings.json), which already grants `Edit(**)`, `Bash(gh:*)` and `defaultMode: acceptEdits`. So the run's real grant is every `gh` subcommand, not the few named ones, and the prompt's "do not edit the issue body" is a request rather than an enforced boundary. Narrowing it properly means changing settings that govern every local session too, which is separate work.
- **Snapshotting the issue bypasses the action's sanitiser.** Keeping the sentinel intact means the raw, attacker-authorable issue body reaches the model unsanitised while it holds a token with `issues: write`. The label gate is the mitigation: an untrusted issue only reaches the model if a maintainer deliberately labels it.

**Date:** 2026-09-07
