# A review rating is raised, never lowered, and old-code bugs are reported, not fixed

**Supersedes:** [2026-10-08-last-review-round-only-reads.md](2026-10-08-last-review-round-only-reads.md) — "Materiality is the reviewer's rating, not the orchestrator's". The reviewer's rating is now a floor: the orchestrator may raise it, in any round, and still may not lower it.

**Intent:** Close the ways `/verify` could report a branch verified when it should not, or stop for the wrong reason, that #346 found in how a finding's severity decides the verdict.

**Behaviour:** [`.claude/commands/verify.md`](../../.claude/commands/verify.md): the reviewer brief and the rules table with its definitions in step 2, and the format-pass branch of step 3.

**Constraints:** The reviewer agents come from a plugin, so their own prompts cannot be edited; the scale can only be asked for in the brief `/verify` gives them.

**Decisions:**

- *No mapping from a reviewer's own scale.* A confirmed finding rated "HIGH" or "MEDIUM", or not rated, takes the Important row and is reported as unrated. A translation table was tried on #346's first attempt and became its own source of review findings (case, numeric bands); treating every foreign rating as Important removes the judgement instead of specifying it.
- *A confirmed defect in the branch's own work is at least Important, in every round.* Raising it only in the last round would fail a run over a defect the earlier rounds were allowed to skip.
- *The ban on lowering a rating applies in every round, not only the last.* It was round-four only, which left the early rounds free to call a real Blocker "mislabelled", skip it, and reach a clean round.
- *A Blocker in code the branch did not touch is deferred in every round, as an Important already was.* It was fixed-or-stop in rounds one to three and deferred in round four, so the outcome depended on which round met it. Stopping in every round was the consistent alternative, rejected because a bug already on `main` would then park an unrelated branch. The cost accepted: a known serious bug in old code no longer stops a run; it is named in the report.
- *A round the formatter wholly undoes is re-sorted, with no per-edit bookkeeping.* #346's first attempt added a record of which edit answered which finding, to detect a formatter reverting a real fix; three review rounds found holes in it. Instead, a finding that was only about layout the formatter governs is rejected as overruled, and any other finding whose fix was undone stops the run as one that could not be fixed. A formatter that undoes only some of a round's edits is not detected, as before.
- *Out of scope has to be shown, not assumed.* Once a Blocker can be deferred, calling a finding out of scope is a way past it, so a finding is out of scope only when the problem has been checked to be on `main` unchanged; a file the branch did not edit is not enough.
- *Carrying deferrals into the pull request was dropped.* `/raise-pr` still starts cold and reads nothing from `/verify`'s report (#346's confirmed decisions).

**Date:** 2026-10-09
