# A confirmed decision rewrites the body passages it contradicts

**Intent:** Stop an issue leaving the pre-build gate contradicting itself, so `/build` reads one specification rather than two.

**Behaviour:** Specified by [#338](https://github.com/FrankRay78/NetPace/issues/338) and by step 4 of [`.claude/commands/confirmissue.md`](../../.claude/commands/confirmissue.md).

**Constraints:**

- The body is the author's text — the rule against tidying the original proposal still holds, so a rewrite fires only where a decision contradicts a passage.
- Nothing may fire on a Pattern A decision: accepting a recommendation as written contradicts nothing.
- Every rewrite has to be listed in the report, because the command's own run is the only review this edit gets — and on the unattended path that report is the Actions run summary, so nothing notifies the author.

**Decisions:**

- **Rewriting the body beats annotating it.** Rejected: leaving the overruled text in place with the decision appended below. That is the state #267 reached, and the author had to ask for the two stale passages to be rewritten by hand.
- **Pattern B and C only.** A rider changes the recommendation, so it can contradict the body just as a redirect does; the earlier reading that only an outright rejection could was wrong, and #267's rider on the removed-switch error is the case that showed it.
- **An out-of-scope answer is a redirect, not a third path.** The passages promising the dropped work are rewritten or removed, which keeps one rule where two would drift apart.
- **The report lists every rewrite.** Rejected: a diff summary or a silent edit. The author is the only check on whether a rewrite read the contradiction correctly, and they will not re-read a body they did not know had changed.

**Date:** 2026-10-10
