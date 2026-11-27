# Review Agents Run A Strict Allowlist

**Intent:** Nothing PR text, an issue or a reviewer's reply can get a CI agent to run may write a file a later command reads as config, git's above all, whose config names programs a later git call runs with the job's tokens.

**Behaviour:** See FrankRay78/gaffer-claude#71, which hardens gaffer's copies of these workflows in step. `review-pr.yml` and `review-followup.yml` run the agent in `default` permission mode, write only under `ci-scratch/` and deny git's `--output`. `claude.yml` denies `git config`, `git -c` and `--output`. `review-pr.yml` has no `workflow_dispatch`.

**Constraints:** Since claude-code-action started agents in auto mode, a safety review can pass any command no rule covers, so an allowlist alone is a floor. On a dispatch the action restores nothing from the base branch, so a fork PR's own hooks would run with the secrets loaded.

**Decisions:**

- Reviews in `default` mode, which loses them nothing. Rejected for `@claude`: it would stop it running the tests and scripts, so it keeps auto mode with hard denies.
- Re-run a review from the Actions tab. Rejected: keeping the dispatch.

**Date:** 2026-11-27
