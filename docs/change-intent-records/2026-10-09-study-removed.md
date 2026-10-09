# The study mechanism is removed, and nothing replaces it

**Supersedes:** [2026-09-14-chain-raises-pr-unattended.md](2026-09-14-chain-raises-pr-unattended.md) — the gate list in *Decisions*. The pull request is no longer reachable past "both study passes", because there are none; `/verify`'s green suite and review are now the whole gate before `/raise-pr`.

**Intent:** Stop paying chain time and chain failure modes for records that never produced usable guidance. Issue #334 has the evidence, from a `/study-review` run on 2026-10-08: of 163 rows across the 19 records then present, 111 were Execution and most of those opened `Reviewer (…):` — an in-branch `/verify` finding caught and fixed in the same run, which is the harness working rather than a surprise. Two rows recorded something that got past `/verify`.

**Behaviour:** `scripts/chain.sh` runs build, verify, raise-pr and counts stages out of three. `/study`, `/study-review` and `docs/study/` no longer exist.

**Constraints:** Dated change-intent records that mention study stay as written; this record supersedes the decisions they carry rather than rewriting history.

**Decisions:**

- *A complete rip-out, not a relocation.* Two replacements were weighed and declined in #334: a weekly workflow studying merged PR reviews, and a trawl of the chain VPS transcripts. Each would re-fund the same cost before anyone had shown a gap that evidence would close. If escaped-review evidence is wanted later, it needs its own issue and a case made from a real gap.
- *`/study-review`'s score was the tell, not just the row mix.* Because several reviewers read every branch, almost any cluster spanned four or more issues and contained one actioned Blocker, so recurrence × cost saturated: three proposals tied at the maximum of 9 and the score ranked nothing. A scoring function that cannot separate its inputs is not measuring them.
- *`STAGE_SESSION` stays; `run_stage`'s `resume` parameter goes.* Only the study passes ever resumed a session, but `fail()` still prints `claude --resume <id>` for the stage that stopped, so the id is still captured from the reply. The two "reply carried no session id, so the study pass could not resume it" hard failures are gone with the passes that needed them — a missing id now costs the resume hint, not the run.
- *The chain's failure scan stays anchored to the start of a line.* Its original reason was `/study` quoting `FAILED reason=` in prose, which no longer happens; the anchor stays because any stage's report may quote the phrase while describing what it found, and `chain.tests.sh` keeps a case proving it.
- *`docs/study/` is deleted outright rather than archived.* Git history keeps all 22 records; a folder retained "for reference" is a folder the next reader has to decide the status of.

**Date:** 2026-10-09
