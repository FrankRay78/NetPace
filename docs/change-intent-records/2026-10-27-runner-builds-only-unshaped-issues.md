# Runner Builds Only Issues Nobody Else Has Written On

**Intent:** Stop text written by anyone but the maintainer from being built unattended on the maintainer's machine, while contributors stay free to raise issues and comment.

**Behaviour:** Specified in [#444](https://github.com/FrankRay78/NetPace/issues/444).

**Decisions:**

- **Rejected: filtering review comments by author in `/confirmissue` and `/reviewissue`** ([#445](https://github.com/FrankRay78/NetPace/pull/445), closed). It added a rule to both commands and still left other routes open. The runner is the one place every route has to pass through before code runs.
- **Rejected: skipping a `ready` issue that still carries a review comment.** It relied on the genuine review surviving, but a stranger's marker comment posted before any review stops one being posted, and is then folded in and deleted with nothing left behind.
- **Accepted: a contributor's comment on the maintainer's issue stops the runner building it.** GitHub keeps the author of a deleted comment but not its text, so a harmless comment cannot be told from a fake review. The maintainer builds such an issue by hand.
- **Accepted: a stranger who deletes their own comment leaves no record.** That matters only if they delete it between a confirmation folding it in and deleting it.

**Date:** 2026-10-27
