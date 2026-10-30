# Review-pr Keeps Its Bindings In Place

**Intent:** Settle once that `/review-pr` is exempt from the *Project-specific* section convention, so a review of a PR that edits the command stops raising it.

**Behaviour:** `.claude/commands/review-pr.md` has no *Project-specific* section; its NetPace bindings stay in the rules that use them. `CLAUDE.md`'s rule on harness command specifics names the exemption and links here.

**Constraints:**

- The convention "applies to … existing ones when next edited", and #455 only deferred `/review-pr` ("its existing specifics are left for their own change"), so every later edit read as an outstanding gap — the first review of #472 raised it as a Pre-existing finding.

**Decisions:**

- The bindings stay in the steps. Rejected: moving them into a *Project-specific* section, as `/build` and `/verify` did. Their bindings are tools and paths, which a step names just as well by role ("run the formatter"). `/review-pr`'s are judgement criteria — what makes a change High tier (server selection, measurement), which paths always need a person (`src/Directory.Packages.props`, public `NetPace.Core` members), what counts as a contract (CLI options, CSV/JSON fields, the `README.md`/`USER_GUIDE.md` update), and the pre-1.0 stance on breaking changes. Lifting them out leaves each rule either abstract ("the repo's high-risk areas") or duplicated in the section, and a reviewer judging a severity or a tier then reads the rule away from the examples that calibrate it. The command is reviewed for quality on this repo, not for portability.
- Accepted: a port of `/review-pr` rewrites its contract, tier and dimension text in place. The call-chain trigger and brief are the parts written to port unchanged ([`2026-10-29-call-chain-trigger-searches-the-name-callers-use`](2026-10-29-call-chain-trigger-searches-the-name-callers-use.md)).
- The exemption lives in the `CLAUDE.md` rule, not in the command. The rule is what reviewers check the command against, so the exemption has to sit beside it.

**Date:** 2026-10-29
