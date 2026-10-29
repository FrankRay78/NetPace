# Review Rounds Record What They Cost

**Intent:** Make the spend of a PR's review cycle readable off the PR, so changes to the review rules can be judged against what they cost.

**Behaviour:** See [issue #453](https://github.com/FrankRay78/NetPace/issues/453). `scripts/pr-cost-footer.sh` writes one row per round into a table at the foot of the PR description; `review-pr.yml` and the new `review-followup.yml` call it after their own "the review posted" check.

**Constraints:**

- A round that posted nothing — a failed run, or a follow-up the three-round cap refused — must leave the description untouched, so the footer records reviews rather than runs.
- Only rounds run in GitHub Actions are measurable; a round run by hand adds no row.

**Decisions:**

- Follow-ups run from the `review-followup` label. Rejected a script that runs the round headless locally: an interactive session reports no totals of its own, so a hand-run round could not be measured the same way as the first review.
- Both workflows share the `review-pr-<n>` concurrency group. Groups are repo-wide, so the shared *name* is what stops two runs each reading the description before the other writes it.
- The cap is re-checked in the workflow, not left to the command. The command's own refusal posts nothing, which the verify step would report as a failed run; the guard makes that outcome a green run with the label cleared.
- Rows are read back from the rendered table, not from state hidden in a comment. A hidden total can disagree with the figures a reader sees, and `Total` must be the sum of the rows as displayed.
- The next row's name comes from the rows already written, not the round number. A re-dispatched first review then adds a row rather than overwriting one.
- Both workflows run main's copy of `scripts/pr-cost-footer.sh`, never the PR head's. The step holds the job's write token, and the head can be written by anyone with push access, or by a fork on a dispatch, so gating on who raised the PR would not cover who wrote its commits. Same reasoning as the runner's rule in [`2026-10-27-runner-builds-only-unshaped-issues`](2026-10-27-runner-builds-only-unshaped-issues.md): trust what the maintainer has merged, not who triggered the run. Accepted: a change to the script takes effect on merge, so it cannot run on its own pull request.
- An unreadable `execution_file` yields `unknown`, not a guess: the review has already posted, and a plausible wrong figure is worse than an admitted gap.

**Date:** 2026-10-28
