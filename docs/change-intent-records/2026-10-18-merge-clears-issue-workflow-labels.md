# A merge clears the issue-workflow labels

**Intent:** Stop closed issues carrying a label that says they are still somewhere in the draft → review → confirm → build chain, so a label view is not polluted by the issues a merge closed, without anyone tidying up after each merge. An issue closed any other way is out of scope and keeps its labels.

**Behaviour:** Specified by [issue #408](https://github.com/FrankRay78/NetPace/issues/408), implemented in `.github/workflows/clear-workflow-labels.yml` and `scripts/clear-workflow-labels.sh`.

**Constraints:**

- Only the seven chain labels may be touched; `bug`, `feature` and the rest survive the merge.
- A pull request closed without being merged must change nothing, and an issue it merely references must keep its labels.
- The run happens after the merge, so a failed label write is not worth failing it for. A query that cannot be answered still is: nothing can be decided without one.

**Decisions:**

- The deciding lives in a script with its own `*.tests.sh` matrix rather than inline in the workflow as `pr-issue-link.yml` does, because the outcomes here — merged or not, in this repository or not, labelled or not, write failed or not — are each worth proving, and shell-tests.yml gates a committed matrix by name for free.
- The merged/not-merged rule sits in the script, not in the workflow's `if:`, so a test can reach it; `scripts/clear-workflow-labels.sh` records why.
- Rejected `pull_request_target`, which would run the default branch's copy and carry a write token even for a fork's pull request. Accepted instead that a fork is skipped outright and that a branch cut before this merged clears nothing: NetPace's branches all live in this repository, and the staleness fixes itself as branches are cut. Not worth the standing hazard of a base-ref workflow in a public repository.
- A failed label write is reported as a run warning and otherwise ignored, rather than collected and reported on a failed run. A label left behind is a tidy-up missed, not damage — but a write that fails for every issue, forever, has to be visible without opening the log of a green run.

**Date:** 2026-10-18
