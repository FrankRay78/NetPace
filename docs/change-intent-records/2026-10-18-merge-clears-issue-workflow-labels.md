# A merge clears the issue-workflow labels

**Intent:** Stop closed issues carrying a label that says they are still somewhere in the draft → review → confirm → build chain, so a label view answers with open issues only without anyone adding `is:open` or tidying up after each merge.

**Behaviour:** Specified by [issue #408](https://github.com/FrankRay78/NetPace/issues/408), implemented in `.github/workflows/clear-workflow-labels.yml` and `scripts/clear-workflow-labels.sh`.

**Constraints:**

- Only the seven chain labels may be touched; `bug`, `feature` and the rest survive the merge.
- A pull request closed without being merged must change nothing, and an issue it merely references must keep its labels.
- The run happens after the merge, so there is nothing left to protect by failing it.

**Decisions:**

- The deciding lives in a script with its own `*.tests.sh` matrix rather than inline in the workflow as `pr-issue-link.yml` does, because `pr-issue-link.yml` has one branch and this has several — merged or not, in this repository or not, labelled or not, write failed or not — and shell-tests.yml gates a committed matrix by name for free.
- The merged/not-merged rule sits in the script, not in the workflow's `if:`, so a test can reach it. The `if:` would have put it where only a real merge could exercise it, and asking GitHub for `merged` costs one field in a query the script already makes.
- Rejected `pull_request_target`, which would run the default branch's copy and carry a write token even for a fork's pull request. Accepted instead that a fork is skipped outright and that a branch cut before this merged clears nothing: NetPace's branches all live in this repository, and the staleness fixes itself as branches are cut. Not worth the standing hazard of a base-ref workflow in a public repository.
- A failed label write is logged and otherwise ignored, rather than collected and reported on a failed run. A label left behind is a tidy-up missed, not damage.

**Date:** 2026-10-18
