# Every pull request closes an issue

**Intent:** Stop work reaching `main` through a pull request that names no issue, so the chain from acceptance criteria to merged code has no hole at its last hop.

**Behaviour:** The `issue-link` job in [`pr-issue-link.yml`](../../.github/workflows/pr-issue-link.yml) fails a pull request unless GitHub says merging it closes an open issue in this repository; `dependabot[bot]` is the only exemption.

**Constraints:**

- `issue-link` is a fixed job name in its own workflow, with no `paths:` or `if:`, because it is meant to become a required status check and a skipped job satisfies one.

**Decisions:**

- **GitHub is asked what the merge closes (`closingIssuesReferences`); the body is not parsed.** *Rejected: a script matching closing keywords in the body.* That was built first: 439 lines of bash and awk re-implementing GitHub's Markdown rules to tell a real `Closes #N` from one quoted in code, and three review rounds each found bodies it passed that GitHub would not link.
- **A Development-sidebar link counts.** This overturns issue #332's confirmed decision that only a keyword in the body counts. *Rejected: also searching the body for the keyword,* which keeps a script and a test matrix alive to exclude a link that does close the issue on merge.
- **Any closing keyword GitHub honours counts**, not only the `Closes`/`Fixes`/`Resolves` the issue named — a consequence of asking GitHub rather than a separate choice.
- **The two ruleset changes are post-merge steps, done by hand:** registering `issue-link` as a required check and deleting the repository-admin bypass actor in `Main CI/CD`. *Rejected: changing the live ruleset from the branch* — requiring a context whose workflow is not yet on `main` blocks every other open pull request.
- **No test matrix.** The check is a dozen lines of workflow shell around one API call, so the gate itself is the test (Constitution §I, tooling carve-out); the pull request that added it records the step failing and passing against real pull requests.

**Date:** 2026-10-09
