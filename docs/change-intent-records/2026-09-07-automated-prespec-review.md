# Running the Pre-Specification Review on Labelled Issues

**Intent:** Take the one step in the spec pipeline whose cost is the *wait* — `/speckit.reviewissue`, which carries the codebase-grounding pass — off the developer machine, so an issue raised away from the desk has its gap analysis waiting rather than requested. Answering the questions and running `/speckit.confirmissue` are quick by comparison and stay local.

**Behaviour:**
- Given: an issue in this repository, and an account with permission to label it
- When: the `review` label is applied
- Then: a single comment appears on that issue containing a numbered pre-specification gap analysis — recommendation and inline answer slot per gap — grounded in real paths and conventions from this codebase; the `review` label is then removed.
- Given: an issue that already carries a `<!-- speckit:review -->` comment
- When: the `review` label is applied again
- Then: nothing is posted and nothing is changed.
- Given: a run that fails for any reason
- Then: no comment is posted and the `review` label stays in place.
- Given: the author has answered the questions inline
- When: `/speckit.confirmissue` is run locally
- Then: the answers fold into the issue body as **Confirmed decisions** with no manual repair — the comment is byte-shape identical to a locally-posted one.
- Given: `@claude` is mentioned on an issue or a pull request
- Then: behaviour is exactly as it was before this change.

**Constraints:**
- The review logic must stay single-sourced in [`.claude/commands/speckit.reviewissue.md`](../../.claude/commands/speckit.reviewissue.md). A workflow that restates it forks the analysis and the two copies drift.
- The repository is public, so the trigger must gate on the account that *applied* the label, not the account that raised the issue.
- Workflows execute the default branch's copy of any file they read, so a change to the review command takes effect on merge, not on branch — and no event can trigger a *new* workflow file from a feature branch at all.
- `/speckit.confirmissue` locates the review by the `<!-- speckit:review -->` sentinel and takes the most recent marker comment. A second automated review would therefore orphan answers already written against the first.

**Decisions:**
1. **A new workflow file, not an extension of `claude.yml`** — `claude.yml` is the `@claude` mention path with narrow permissions scoped to that job. Folding a second trigger into it would widen those permissions for the mention path too, and couple two automations that fail independently. Rejected: adding `issues: [labeled]` to the existing workflow.
2. **A label is the trigger, not issue-opened** — bugs and trivial changes do not warrant the overhead, so reviewing is opt-in. Labelling already requires triage or write permission, which makes the trigger self-gating and removes the need for a separate actor allowlist; the `sender.login` check is belt-and-braces on top of that. Rejected: reviewing every issue on open (deferred as a later decision once the marked path has proven itself).
3. **The workflow snapshots the issue in a plain `gh` step, rather than granting Claude `Bash(gh issue view:*)`** — the action injects issue context on the `issues` event but not on `workflow_dispatch`, so relying on injection would leave the dispatch path unable to read the issue at all. A snapshot step makes both trigger paths identical and keeps Claude's grants to exactly the two writes it needs: `Bash(gh issue comment:*)` and `Bash(gh issue edit:*)`. Rejected: injection-only (breaks dispatch); a third `gh issue view` grant (widens the tool surface for no gain).
4. **Label removal is the done-marker, and there is no failure comment** — after a successful post the workflow removes `review`, so the label set carries the state: labelled means pending, unlabelled with a review comment means done, still-labelled with no review comment means the run failed. GitHub's failed-workflow notification is the alert. Rejected: a posted failure comment (noise on the issue for a signal GitHub already sends).
5. **No CI-specific footer on the comment** — the comment carries the command's own footer, which already tells the reader to re-run `/speckit.reviewissue` when a question needs expanding. A CI-only warning was considered and dropped: sole developer on this repository, so the guard would only make every comment more verbose. The re-run guard in decision 4 covers the actual hazard.
6. **`timeout-minutes: 30`, no `--max-turns`** — standard runners are free on a public repository and turns are not metered per unit on this plan, so only a hung job is worth guarding. `--max-turns` is the one limit that could silently shorten the codebase-grounding pass and quietly degrade the review into generic advice, which is exactly the failure this automation exists to avoid.
7. **`workflow_dispatch` alongside the label trigger** — a new workflow file cannot be triggered by an event from a feature branch, so the Principle I evidence for this change is merge-then-verify: the real label trigger observed working on `main` against a throwaway issue. `workflow_dispatch` earns its place afterwards, by making every later prompt tweak runnable against a branch ref instead of another merge.

**Known residual:** refining an existing review in place (step 6 of the review command) remains local-only. It edits a review already written and answered, and the re-run guard in decision 4 deliberately blocks the automated path from touching an issue that already has one.

**Date:** 2026-09-07
