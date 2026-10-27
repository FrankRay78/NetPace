# Runner Builds Only Issues Nobody Else Has Written On

**Intent:** Stop text written by anyone but the maintainer from being built unattended on the maintainer's machine, while contributors stay free to raise issues and comment.

**Behaviour:** Specified in [#444](https://github.com/FrankRay78/NetPace/issues/444).

**Decisions:**

- **Rejected: filtering review comments by author in `/confirmissue` and `/reviewissue`** ([#445](https://github.com/FrankRay78/NetPace/pull/445), closed). It added a rule to both commands and still left other routes open. The runner is the one place every route has to pass through before code runs.
- **Rejected: skipping a `ready` issue that still carries a review comment.** It relied on the genuine review surviving, but a stranger's marker comment posted before any review stops one being posted, and is then folded in and deleted with nothing left behind.
- **Accepted: a contributor's comment on the maintainer's issue stops the runner building it.** GitHub keeps the author of a deleted comment but not its text, so a harmless comment cannot be told from a fake review. The maintainer builds such an issue by hand.
- **Accepted: a stranger who deletes their own comment leaves no record.** If a confirmation folds their comment in but fails to delete it, the `confirm` run goes red and the runner skips the issue while the comment is there. Once they delete it themselves, the issue is built, so after a red `confirm` run the maintainer checks the decisions and deletes that comment by hand. A comment the review agent read and its author then deleted leaves no record either, but what it shaped reaches the body only through a review the maintainer answered.

**Date:** 2026-10-27
