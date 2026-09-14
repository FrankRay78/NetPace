# The command chain opens the pull request without a human pause

**Intent:** Let one invocation of `scripts/chain.sh <issue>` carry an issue from a clean `main` to an open pull request — `/build` → `/study` → `/verify` → `/study` → `/raise-pr` — with nobody reading a report and starting the next stage. Typed by hand, most of the chain's wall-clock is spent waiting for that person, not doing the work (#270).

**Behaviour:**
- Given a ready issue and a clean `main`, when the chain runs and every stage reports its own success verdict, then the last stage pushes the branch and opens the pull request with no prompt at any point.
- Given any stage fails, stalls or reports no recognisable verdict, when that stage ends, then no later stage — `/raise-pr` included — starts, and the closing message names the stage and the reason.
- Given `/verify` is invoked on its own, when it completes, then it still pushes nothing and opens no pull request; only a run of the chain, or a separate `/raise-pr`, does.

**Constraints:**
- This reverses a stated principle. [The verify/raise-pr split](2026-09-07-verify-raise-pr-split.md) rejected auto-invoking `/raise-pr` because "the outward-facing step is *always* deliberate", and the generic guide says to stop before the irreversible step. Both documents stay as written for the commands; this record is the named exception.
- The deliberate human act does not disappear, it moves: from raising the pull request to starting the chain against one named issue. That start is guarded before any model time is spent — an issue must be named, the tree clean and `main` checked out — and `--dry-run` shows the five stages without running any.
- The pull request is only reachable past every earlier gate: `/verify`'s green suite and review, and both study passes. The chain enforces that ordering from outside the model, by exit code and verdict, not by the model's say-so.
- Silently denied `ask` rules in headless runs remain a documented residual risk: a stage can carry on degraded and still report success, and the chain does not detect it.

**Decisions:**

*Rejected — pausing for confirmation before `/raise-pr`.* It keeps the letter of the split's principle, and it is the option a maintainer rereading that record will reach for first. It loses because a pause at the last stage means someone must be at the terminal when an hour-long run happens to reach it — exactly the waiting the chain exists to remove — and the chain would stop being unattended.

*Rejected — ending the chain at `/verify` and leaving `/raise-pr` by hand.* Safe, but it leaves the most mechanical stage as the one manual step, and #270's confirmed decision was an issue-to-pull-request chain.

*Why this is not the flag the split rejected.* That flag made a pull request a side effect of a *verification* run — the same command, sometimes outward-facing, indistinguishable at the call site. The chain is a separate command whose stated and only purpose is to end at a pull request, so invoking it is the deliberate choice to open one. `/verify` itself keeps its guarantee.

*Adopted — automate the final stage, and keep the commands separate.* Running `/verify` and `/raise-pr` by hand still gives the pause for anyone who wants it.

**Date:** 2026-09-14
