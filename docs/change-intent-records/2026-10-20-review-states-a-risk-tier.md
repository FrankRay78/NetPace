# The review states a risk tier and a no-human-merge verdict

**Intent:** Have every PR review say how much human attention the change needs, so the maintainer is not judging each PR from scratch — and gather the evidence that would justify auto-merging later.

**Behaviour:** `.claude/commands/review-pr.md`, *Risk tier and human review*, specifies the tier, the always-a-person paths, the verdict and the fixed line; `.claude/commands/review-followup.md` step 7 carries both forward. Scenarios and criteria are in issue #423.

**Constraints:**

- Nothing is merged automatically. The verdict is posted and read, and the maintainer still merges.
- NetPace has one maintainer, so the source process's one-person and two-person approval tiers collapse into a single "maintainer review" outcome.
- No `dotnet test` can exercise prompt text. Constitution §I's configuration-and-tooling carve-out applies: the real tool is the review itself, road-tested with `/review-pr 406 --dry-run`.

**Decisions:**

- Post the tier before anything acts on it, because the source process says to start strict and loosen with evidence — a record of what the review *would* have let through is what shows whether the tiers match the maintainer's judgement.
- A finding tagged **Pre-existing** does not block the verdict; every other open finding, of any severity, does. Rejected blocking on pre-existing findings: it parks the verdict on bugs the PR did not introduce, which the author cannot be asked to fix to earn a merge.
- The tier and the merge recommendation stay two independent decisions, not one scale. The recommendation answers what the review found; the tier answers what the change touches. A **Merge** with no findings can still require maintainer review, and folding them together would lose one of the two answers.
- `/review-followup` judges the tier and the always-a-person check on the whole PR diff against the base at the current head, not on the delta it reviews. Rejected judging the delta: a PR reviewed Low would keep its **yes** after a later commit touched a release workflow, because that commit alone is small.
- The always-a-person list names paths, not file counts or diff sizes, so the check is answerable by looking at the changed-file list and cannot drift with a reviewer's sense of how big a change feels.

**Date:** 2026-10-20
