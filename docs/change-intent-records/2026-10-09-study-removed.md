# The study mechanism is removed, and nothing replaces it

**Intent:** Stop paying chain time and chain failure modes for a mechanism that never produced usable guidance. Issue #334 has the evidence.

**Behaviour:** `scripts/chain.sh` runs build, verify and raise-pr, and counts stages out of three. `/study`, `/study-review` and `docs/study/` no longer exist.

**Constraints:** The `docs/study/` records cited as evidence now live only in git history.

**Decisions:**

- *A complete rip-out, not a relocation.* Two replacements were weighed and declined in #334: a weekly workflow studying merged pull-request reviews, and a trawl of the chain VPS transcripts. Each would re-fund the same cost before anyone had shown a gap that the evidence would close.
- *When to revisit.* Only on a real gap. If escaped-review evidence is wanted later it needs its own issue, and the case has to be made from a finding that actually got past `/verify` — not from the absence of evidence that there are any.

**Date:** 2026-10-09
