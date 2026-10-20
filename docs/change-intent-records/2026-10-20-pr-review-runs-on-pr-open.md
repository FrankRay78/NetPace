# The PR review runs when the PR opens

**Intent:** Replace the free-form `@claude` review that `/raise-pr` requested with the multi-pass, verified `/review-pr`, and have it review every pull request the maintainer opens, however it was raised.

**Behaviour:** `.claude/commands/review-pr.md` and `.claude/commands/review-followup.md` specify the review; `.github/workflows/review-pr.yml` runs the first round on PR open.

**Constraints:**

- The chain's `/raise-pr` stage must stay unattended and must not lose its `RAISED` verdict once a PR exists.
- Both commands run to posting with no human confirmation and post whoever authored the PR — the maintainer authors nearly every PR, so an author check that suppressed posting would suppress almost every review.

**Decisions:**

- Rejected calling `/review-pr` as a final `/raise-pr` step. It would put a multi-agent review inside the 30-minute `raise-pr` stage limit, and a review failing after the PR opened would need its own rule to keep the `RAISED` verdict. A PR raised by hand or in the GitHub UI would also go unreviewed.
- Rejected keeping the `@claude` mention and pointing it at the command file: `claude.yml` grants the bot no tool allowlist suited to a multi-agent review, and the mention only exists when `/raise-pr` posts it.
- One round per PR, on `opened` and `ready_for_review`, not on every push: re-reviewing the whole PR on each push is what `/review-followup` is designed to avoid, and follow-up rounds stay a deliberate act.
- The review contract (severities, what not to flag) is inlined in `review-pr.md` rather than kept in a separate `REVIEW.md`, so the one command file is the whole specification.

**Date:** 2026-10-20
