# A Review Reason Survives Being Checked

**Intent:** Make every factual claim in a `/speckit.reviewissue` recommendation's *Reason* true when checked against the file, command output or issue text it names — so an author accepting a recommendation from the at-a-glance table, with little reading, is not quietly settling a decision on evidence that does not exist.

**Behaviour:**

- Given a Reason that cites a file, a search or the issue's text, When the review is composed, Then the claim is re-checked against that source before the review posts, first run or refine-run edit alike.
- Given a Reason whose claim does not hold, or cannot be checked with the run's tools, When the check runs, Then the Reason is recast to say it is the reviewer's judgement, the Recommendation is kept, and the review still posts.
- Given a recommendation with no checkable evidence behind it, When the author reads it, Then its Reason says plainly that it is the reviewer's judgement, so an opinion is distinguishable from an evidenced call.

**Constraints:**

- Whether a recommendation is a good default is out of scope; only the truth of the evidence behind it is checked.
- The same prompt runs locally and in `speckit-reviewissue.yml`, whose tool allowlist is `Read`, `Glob`, `Grep`, `Write` and two `gh` commands. The rule therefore names what to check against, not a tool to check it with, and treats a claim the run cannot check as unbacked.
- Prompt-only change, so Principle I's configuration-and-tooling carve-out governs. As in CIR [`2026-09-12-triageable-review-comment`](2026-09-12-triageable-review-comment.md), the invariant lives in a comment composed at runtime, so it is enforced by the command's own pre-post self-check rather than a gate.

**Decisions:**

1. **The reviewer checks its own claims; no subagent.** Rejected: a verifying subagent, which would also mean adding the subagent tool to the workflow's `--allowedTools`. Re-opening the cited file is cheap for the model that just cited it, and the failure on #270 — "Sonnet is the harness's standing default", with no file in the repository naming Sonnet — was one re-read away from being caught. Rejected with it: batching every Reason into one separate verification call, which has nothing to batch once no separate call exists.
2. **A failed check recasts the Reason; it never blocks posting.** Rejected: withholding the review, or dropping the gap, until its evidence holds. The gap is still genuinely open and the recommendation may still be a good default — what was wrong is the claim that it was evidenced. Recasting keeps the author's answer slot and removes only the false authority.
3. **A claim about how the repository or harness behaves counts as factual.** The #270 Reason read as grounded because it described the harness rather than naming a file. Calling that class out is what stops the check being satisfied by a Reason that simply cites nothing specific.

**Date:** 2026-09-12
