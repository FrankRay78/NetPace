# Reviews Come From Named Accounts

**Intent:** Have the pre-specification review, refine and confirm paths act only on a sentinel comment posted by a named account, so a stranger's copy of a review can't be folded into an issue as its confirmed decisions.

**Behaviour:** Specified in [#444](https://github.com/FrankRay78/NetPace/issues/444).

**Constraints:**

- The review workflow posts as `claude[bot]`, not `github-actions[bot]`: `claude-code-action` posts with its own app token. Every workflow that posts as `claude[bot]` is gated on the maintainer triggering it.
- `gh issue view --json comments` reports that account as `claude`, without the `[bot]` suffix, so the check reads REST's `user.login`.

**Decisions:**

- **Rejected: trusting `author_association`.** `claude[bot]` comes back as `CONTRIBUTOR`, which any account with a merged pull request also gets.
- **Rejected: allowing `github-actions[bot]` too.** No review has been posted as it, and an unused entry widens trust for nothing.

**Date:** 2026-10-27
