# A Review Reason Survives Being Checked

**Intent:** Make every factual claim in a `/speckit.reviewissue` recommendation's *Reason* true when checked against the file, command output or issue text it names — so an author accepting a recommendation from the at-a-glance table, with little reading, is not quietly settling a decision on evidence that does not exist.

**Behaviour:**

- Given a Reason that cites a file, a search or the issue's text, or describes how the repository or harness behaves, When the review is composed, Then the claim is re-checked against the repository or issue before the review posts, first run or refine-run edit alike.
- Given a Reason whose claim is found false, When the check runs, Then the claim is dropped and the Reason rewritten as the reviewer's judgement without it, the Recommendation is kept, and the review still posts.
- Given a Reason whose claim the run's tools cannot check, When the check runs, Then the claim survives only inside a Reason marked as the reviewer's judgement, so an opinion is distinguishable from an evidenced call.

**Constraints:**

- Whether a recommendation is a good default is out of scope; only the truth of the evidence behind it is checked.
- The same prompt runs locally and in `speckit-reviewissue.yml`, whose tool allowlist is `Read`, `Glob`, `Grep`, `Write` and two `gh` commands. The rule therefore names what to check against, not a tool to check it with. One visible consequence: a Reason citing another issue or PR is checkable locally but not in the workflow, which has no `gh issue view`, so the same true claim posts as evidence from a local run and as judgement from an automated one.
- Prompt-only change, so Principle I's configuration-and-tooling carve-out governs. As in CIR [`2026-09-12-triageable-review-comment`](2026-09-12-triageable-review-comment.md), the invariant lives in a comment composed at runtime, so it is enforced by the command's own pre-post self-check rather than a gate.

**Decisions:**

1. **The reviewer checks its own claims; no subagent.** Rejected: a verifying subagent, which would also mean adding the subagent tool to the workflow's `--allowedTools`. Re-opening the cited file is cheap for the model that just cited it, and the failure on #270 — "Sonnet is the harness's standing default", when no file on `main` named Sonnet at the time — was one search away from being caught.
2. **A false claim is dropped; an unbacked recommendation is kept.** Rejected: withholding the review, or dropping the gap, until its evidence holds. The gap is still genuinely open and the recommendation may still be a good default — what was wrong is the claim that it was evidenced. Also rejected: merely prefixing the failed claim with a judgement label, which would leave the false statement in front of the author with a hedge attached.
3. **A claim about how the repository or harness behaves counts as factual.** The #270 Reason read as grounded because it described the harness rather than naming a file. Such a claim is checked by searching for where the behaviour would be set, and finding nothing fails it — otherwise the check is satisfied by any Reason that simply cites nothing specific.

**Date:** 2026-09-12
