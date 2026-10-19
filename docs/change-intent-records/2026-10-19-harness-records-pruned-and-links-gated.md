# Harness records are pruned to what is written nowhere else, and links are gated

**Intent:** Keep `docs/change-intent-records/` holding reasoning that would otherwise be lost, rather than a log of superseded harness designs. A record is point-in-time and never updated, so 27 harness records had accumulated mechanisms that no longer exist — the issue-review automation alone went through five records in five weeks.

**Behaviour:** Issue #415 carries the record-by-record plan and the classification it came from.

**Decisions:**

- *The test applied to each record was where its live reasoning is written, not how old it is.* Delete where the still-true reasoning sits beside the rule it governs — in the command, hook or workflow file — or in one of the two workflow docs. Trim to the rejected alternative or constraint written nowhere else. Two records went because the mechanism itself is gone.
- *Rejected — keeping a deleted record as history under a "superseded" banner.* That is exactly what the two superseded records already carried, and an agent reading one still met `/verify`'s three-round review loop before it met the banner saying so. A record read as history costs the same to read as one read as current.
- *Rejected — an off-the-shelf markdown link checker.* `lychee` and `markdown-link-check` both resolve markdown links well, and neither sees the shape that actually goes stale here: a bare `docs/change-intent-records/<name>.md` path in prose or a YAML comment, which is how the constitution and two workflows cite a record. They would also add a toolchain to the runner for a check that needs only `git` and `grep`.
- *Inbound links are redirected, never left to resolve by accident.* Each pointer to a deleted record now names the prose that holds the reasoning. `scripts/link-check.sh` is what makes that checkable, and its `link-check` CI job is what keeps the next deletion honest.

**Date:** 2026-10-19
