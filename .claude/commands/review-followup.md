---
description: Follow-up round on a PR previously reviewed with review-pr — evaluate author replies, verify fixes in new commits, resolve or re-raise threads, re-review the new diff only, update the recommendation. Runs unattended to posting. Caps at three rounds.
argument-hint: "[PR number or URL] [--dry-run]"
---

# review-followup

Run a follow-up round on a pull request already reviewed with `/review-pr`. It closes the loop with the author without the reviewer hand-writing replies: each open thread gets evaluated and answered, fixes get verified against the new commits, and only the new diff gets re-reviewed. Review must converge — this command never re-litigates the whole PR. Like `/review-pr`, it runs unattended from start to post and never stops for confirmation.

## User Input

```text
$ARGUMENTS
```

A PR number (`158`), `#158`, or a full PR URL; empty resolves the current branch's PR as `/review-pr` does. `--dry-run` prints everything instead of posting.

## Steps

### Gather state

1. **Resolve the PR and prior rounds.** `gh pr view <N> --json number,url,state,author,headRefName,headRefOid,baseRefName`; stop if not `OPEN`. Find prior rounds: the PR's reviews (`gh api --paginate repos/{owner}/{repo}/pulls/<N>/reviews`) whose body carries the `<!-- review-pr -->` marker. Match on the marker, not the reviewer's login — the first round usually posts from CI as a bot, and a follow-up run by hand posts as a person. If none, stop: "No prior review-pr round on PR #<N> — run /review-pr first."

2. **Round cap.** This run is round `prior + 1`. If it would be round 4 or later, post nothing: report that the cap is reached and recommend a direct conversation with the author about the approach — more rounds past three means the disagreement is about direction, not defects.

3. **Fetch the threads.** Via GraphQL (`gh api graphql`), get the PR's `reviewThreads` with `id`, `isResolved`, `path`, `line` and each thread's comments (`databaseId`, author, body, `pullRequestReview { databaseId }`). The threads from prior rounds are those whose first comment belongs to one of the marked reviews from step 1. For each, establish what has happened since: author replied, author pushed commits, or nothing.

4. **Check out the new state.** As in `/review-pr` step 2: in place if `HEAD` is already `headRefOid`, otherwise a detached worktree of `pull/<N>/head` under `.claude/scratch/`, removed when the round completes or aborts. Compute the delta since the last reviewed commit (the `commit_id` of the most recent marked review): `git diff <last-reviewed-sha>..HEAD`. Load the same context and review contract as `/review-pr`.

### Evaluate each open thread

5. For every unresolved thread from a prior round, decide one of:

   - **Fixed** — the new commits address it. Verify against the checkout (a fresh-context verifier subagent confirms the fix actually resolves the claim, not just touches the line). Reply confirming with the fixing commit referenced, and resolve the thread.
   - **Justified** — the author replied with a reason and no code change. Assess the reason on its merits against the contract. If sound, reply accepting and resolve. If not, reply once stating specifically why the justification doesn't hold — never repeat the original finding verbatim.
   - **Unanswered** — no reply, no fix. Re-raise once, politely, in the thread. If it was already re-raised in a previous round, leave the thread alone and escalate it to the summary as still open.

   Replies go via the thread reply endpoint (`gh api repos/{owner}/{repo}/pulls/<N>/comments/<id>/replies`); resolving uses the GraphQL `resolveReviewThread` mutation with the thread id.

### Re-review the delta only

6. **New diff only.** Run the `/review-pr` specialist-and-verify pipeline (Phases 2–3) scoped strictly to the delta from step 4. Never repeat a finding already posted in any round, and never flag code untouched since the last review. Flag the class of any new bug so the author fixes every instance.

### Compose and post

7. **Summary and recommendation.** Compose a short round summary: the `<!-- review-pr -->` marker on its first line, per-thread outcomes (fixed / justified-accepted / still open), any new verified findings (numbered continuing from the previous round's list), the round number, and an updated recommendation (**Merge** / **Merge after addressing** / **Do not merge**) under the same rules as `/review-pr`. Footer: `*Review assisted by Claude Code.*`

8. **Post.** In `--dry-run`, print every reply, resolution and the summary, and stop. Otherwise post thread replies and resolutions first, then new inline findings and the summary as one review (`event: COMMENT`, `commit_id` set to `headRefOid`), as in `/review-pr`.

9. **Report.** Print the PR URL, round number, thread outcome counts, new finding count, the recommendation, and — if everything is resolved and the recommendation is **Merge** — that approving and merging the PR is the maintainer's call in GitHub, not this command's.
