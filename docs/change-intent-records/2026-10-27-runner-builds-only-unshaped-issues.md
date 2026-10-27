# Runner Builds Only Issues Nobody Else Could Have Shaped

**Intent:** Stop text written by anyone but the maintainer from being built unattended on the maintainer's machine.

**Behaviour:** Specified in [#444](https://github.com/FrankRay78/NetPace/issues/444).

**Decisions:**

- **Rejected: filtering review comments by author in `/confirmissue` and `/reviewissue`** ([#445](https://github.com/FrankRay78/NetPace/pull/445), closed). It stopped a stranger's marker comment being folded in, but left their own issue editable after `ready`, and added a rule to both commands. The runner is the one place every route has to pass through before code runs.
- **Accepted: a stranger's fake answers can still reach an issue body.** They are never built, and the `confirm` run goes red, its message saying to check the decisions before deleting the surviving review.

**Date:** 2026-10-27
