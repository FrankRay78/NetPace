---
description: Multi-pass verified review of an open PR — triage map, parallel specialist passes, finding verification, inline comments, a risk tier, a no-human-merge verdict and a merge recommendation. Runs unattended to posting. Supports --dry-run.
argument-hint: "[PR number or URL] [--dry-run]"
---

# review-pr

Review an open pull request in depth and post the results back to the PR: a summary comment with a triage map, a risk tier, a verdict on whether the PR could merge with no human review, and a merge recommendation, plus one inline review comment per verified finding. Designed for large PRs spanning several layers in code the reviewer may not know.

`.github/workflows/review-pr.yml` runs this command on every pull request `FrankRay78` opens, so a run there posts as the workflow's bot. Run by hand, it posts under the invoker's own `gh` account. Either way it runs unattended from start to post: it never stops for confirmation, and it posts whoever authored the PR. Run it on your strongest available model — a missed bug costs more than the review tokens.

## User Input

```text
$ARGUMENTS
```

Optional. A PR number (`158`), a `#158`, or a full PR URL. Empty means review the PR open for the current branch — resolve with `gh pr view --json number,url`; if there isn't one, stop: "No open PR for the current branch — pass a PR number or raise one first."

Flags, anywhere in the arguments:

- `--dry-run` — run everything but post nothing; print exactly what would have been posted. Use for calibrating noise.

## Review contract

**Severity**

- **Critical** — wrong results, data loss, a crash on a supported path, a security hole, or a broken gate (a CI check or hook that passes when it should fail). Blocks merge.
- **Major** — a real defect on a less common path, a missing test for new behaviour, a breach of a constitution MUST, or an unstated breaking change. Should be fixed before merge.
- **Minor** — worth fixing, but merging without it does no harm.

**What not to flag**

- Formatting and style the formatter or `.editorconfig` already settles.
- Anything a required CI check already gates (build, tests, traceability, issue link), unless the check itself is what the PR breaks.
- Breaking changes as such. While NetPace is pre-1.0 they need no approval or deprecation period (Constitution VII). Flag only a PR that does not state what breaks and for whom.
- Preferences with no consequence a reader could name.

## Risk tier and human review

Every review states a risk tier and a yes-or-no verdict on whether the PR could merge with no human review. The tier is set from what the change touches, and neither is ever set from who authored it. Nothing is merged automatically: the verdict is advice about the commit reviewed, not about anything pushed after it, and the maintainer still makes the merge decision on the PR.

**Tier** — one of:

- **Low** — docs, tests or mechanical changes only (renames, moved files, generated code, lockfiles). A changed snapshot is never Low: a snapshot is a user-visible contract, and a changed one means the output changed.
- **Medium** — anything that is neither Low nor High: real logic, or a changed snapshot.
- **High** — logic in server selection or measurement, security-sensitive code, or a change large enough to span several layers.

Only Low is eligible for merge with no human review. Medium and High both mean maintainer review; High marks the changes that most deserve a careful read.

**Always a person.** A change touching any of these needs the maintainer whatever tier the rest of it would get, so it is never eligible, and the summary names the path responsible. Read the changed paths from `git diff --name-only origin/<baseRefName>...HEAD` in the checkout, never from step 1's `files`, which stops at 100 files without saying so:

- anything under `.github/` or `scripts/`
- the release and publish pipeline, including `docs/RELEASING.md`
- the Claude harness — anything under `.claude/` — and `CLAUDE.md`
- `docs/constitution.md`
- `src/Directory.Packages.props`, and package-reference changes in project files
- any change to a contract a user or a consumer relies on: a CLI option, an exit code, a CSV or JSON field, a public `NetPace.Core` member

**The second look.** Even where the areas touched suggest Low, ask whether this particular change still deserves a person. Lift Low to Medium where it does, and say why.

**The verdict** is **yes** only when all three hold: the tier is Low, no always-a-person path is touched, and no verified finding is open other than a **Pre-existing** one below Critical. Any other open finding, of any severity, makes it **no**. A Pre-existing Major or Minor finding is listed and does not block; a Pre-existing Critical blocks, because it still forces **Merge after addressing** (step 9).

**The line.** The summary states both directly under its header, in this wording every time:

`Risk tier: <Low|Medium|High> · Merge with no human review: <yes|no>`

The reasons follow it: what set the tier, the always-a-person path where one applies, and what makes the verdict **no** where it is no.

## Subagents

Every subagent this command starts — triage, specialists, verifiers — follows these rules, and each one's brief repeats the second and third.

- **Run them in the foreground and wait for them.** Start a phase's subagents together in one message so they run in parallel, never in the background, and have every result before starting the next phase. Never end your turn while one is still running: in CI the session ends with your turn, and unfinished work is lost with nothing posted.
- **Investigate by reading.** Use Read, Grep and Glob. Use Bash only for single `git` and `gh` commands run from the checkout root — no pipes, no chaining, no `cd`. A run may refuse anything else.
- **Never run code from the PR.** Its scripts, tests and build are untrusted, and in CI the run holds a token that can write to the PR. CI's own checks already run the PR's tests. Reason from the code instead.

## Steps

### Phase 0 — Resolve and gather context

1. **Resolve the PR.** `gh pr view <N> --json number,url,title,body,state,author,baseRefName,headRefName,headRefOid,files,additions,deletions`. If `state` is not `OPEN`, stop: "PR #<N> is <state> — nothing to review."

2. **Check out the code.** A deep review reads the diff against the whole repository, not in isolation. If the current checkout's `HEAD` is already the PR's `headRefOid` (as it is in CI), review it in place. Otherwise never disturb the invoker's working tree: `git fetch origin pull/<N>/head`, then `git worktree add --detach .claude/scratch/review-pr-<N> FETCH_HEAD`, and review there — both run from the repository root, so neither needs a `cd`. Remove the worktree (`git worktree remove --force .claude/scratch/review-pr-<N>`) when the review completes or aborts.

3. **Load the context.** In the checkout, read `CLAUDE.md`, `docs/constitution.md` and the convention docs `CLAUDE.md` points to for the layers the PR touches (`docs/conventions/csharp-style.md` for `.cs` files, `docs/conventions/testing.md` for tests). Read the issue the PR body closes or references (`Closes #<N>`, `Refs #<N>`) for intent.

### Phase 1 — Triage map

4. **Map the change.** Delegate to a subagent (model: haiku — this is mechanical): group the changed files by area (`NetPace.Core` library, `NetPace.Console` CLI, tests and snapshots, CI workflows and scripts, the Claude harness under `.claude/`, docs), and flag which groups contain real logic changes versus renames, generated code and lockfiles. The map goes in the summary comment — it helps every reviewer of the PR, not just this one.

### Phase 2 — Parallel specialist passes

5. **Hunt in parallel.** Launch fresh-context subagents concurrently, one per dimension, each given the checkout path, the diff, the PR's stated intent and the review contract. Each returns candidate findings with `file:line`, a one-line claim, and the evidence. Dimensions (skip any the triage map shows genuinely don't apply):

   - **Correctness** — logic errors, off-by-ones, null/empty handling, concurrency, cancellation, error paths, silent failures (swallowed exceptions, missing timeouts/retries, catch blocks that hide errors). For CI workflows and scripts: steps that report green when the thing they guard did not happen.
   - **Security** — injection (including untrusted PR or issue text reaching a shell or a prompt in a workflow), workflow permissions and secrets, sensitive data in logs or output.
   - **Contracts** — anything a user, script or `NetPace.Core` consumer touches: CLI options, exit codes, CSV/JSON fields, public library members. A change to one must be stated in the PR body with who it breaks, and a changed CLI option must update `README.md` and `USER_GUIDE.md`. Also reflection-heavy or non-trim-safe code, since NetPace targets AOT-trimmed builds.
   - **Tests** — do tests exercise the new behaviour and its edge cases, or only the happy path? Tests that mock away the system under test, assert mechanism rather than outcome (Constitution IX), or hand-roll string matching where console output belongs in a snapshot.
   - **Conventions and intent** — adherence to `CLAUDE.md`, the constitution and the patterns in touched files, and anywhere the PR body, linked issue or tests disagree with what the diff actually does.

### Phase 3 — Verification

6. **Verify every candidate.** For each candidate finding, a fresh-context verifier subagent must independently confirm it against the checkout: reproduce the reasoning from the cited `file:line`, check the claim survives the surrounding code, and return CONFIRMED or REJECTED with a reason. Only CONFIRMED findings proceed. This is the quality gate — a false positive posted to the author costs trust; never skip verification to save time.

7. **Classify survivors.** Tag each finding with the contract's severity, and tag **Pre-existing** where the problem was not introduced by this PR — a pre-existing bug must not read as the author's fault. Deduplicate: one finding per root cause, noting other instances, so the author fixes the class rather than one line. Drop anything the contract's "what not to flag" excludes.

### Phase 4 — Compose and post

8. **Compose the review.**
   - *Inline comments*: one per finding that sits on a line present in the diff — body gives severity, the claim, why it matters here, and the suggested fix. Findings on lines outside the diff go in the summary instead.
   - *Summary comment*: the marker `<!-- review-pr -->` on its first line (`/review-followup` and the workflow find prior rounds by it); a one-line header naming the PR; the tier and verdict line in the wording fixed above, with its reasons; the triage map; findings as a single continuously numbered list across all severities (1, 2, 3, …) so discussion can reference "item 3" — inline-posted findings appear here as one numbered line linking to their thread; counts per severity; the merge recommendation; and the footer `*Review assisted by Claude Code.*`

9. **Recommendation, tier and verdict.** Decide one of:
   - **Merge** — nothing blocking; remarks are optional.
   - **Merge after addressing** — list the specific items that must be fixed first.
   - **Do not merge** — explain the blocker.

   Any Critical finding forces at least **Merge after addressing**. The recommendation is advice to the maintainer, who makes the merge decision on the PR.

   Then set the tier and the no-human verdict per *Risk tier and human review*. The two decisions are independent — the recommendation answers what the review found, the tier answers what the change touches — so a **Merge** with nothing found can still require maintainer review.

10. **Post.** In `--dry-run` mode, print instead of posting and stop. Otherwise post inline findings and the summary as a single PR review via `gh api repos/{owner}/{repo}/pulls/<N>/reviews --input <file>` with `event: COMMENT`, `commit_id` set to `headRefOid`, the summary as `body`, and `comments[]` entries of `{path, line, side: "RIGHT", body}`. Write the JSON to `.claude/scratch/review-pr-<N>.json` first, so backticks and `$` pass through untouched. If the API rejects an inline position, move that finding's full text to the summary and post again rather than dropping it.

11. **Report.** Print the PR URL, the recommendation, the tier and the no-human verdict, counts per severity, how many candidates verification rejected, and that follow-up rounds run via `/review-followup <N>`.
