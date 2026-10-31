# Review criteria live in REVIEW.md

**Intent:** Separate `/review-pr`'s generic review method from NetPace's own judgement criteria, so the method can later be lifted into a portable plugin whose commands read a repository's `REVIEW.md` where one exists. Refs #477.

**Supersedes:** [`2026-10-29-review-pr-keeps-its-bindings-in-place`](2026-10-29-review-pr-keeps-its-bindings-in-place.md) — its decision that the bindings stay in the rules that use them; and [`2026-10-20-pr-review-runs-on-pr-open`](2026-10-20-pr-review-runs-on-pr-open.md) — its decision that the review contract is inlined in `review-pr.md` rather than kept in a separate `REVIEW.md`.

**Behaviour:** [`REVIEW.md`](../../REVIEW.md), read as [`.claude/commands/review-pr.md`](../../.claude/commands/review-pr.md)'s *Repository criteria* says; `.github/workflows/review-pr.yml` and `review-followup.yml` put `main`'s copy in place before the agent runs.

**Constraints:** The 2026-10-29 record's concern stands — a rule read away from the examples that calibrate it — so each section of `REVIEW.md` sits under the heading of the method's part it extends, the command's heading table names the pairing, and a reviewer reads rule and examples together. The restore is a workflow step, and a workflow file runs from the pull request's own head, so a pull request that edits `.github/` could remove it; the author gate on both workflows is the guard against that, as it already was for the model table and the cost script.

**Decisions:**

- Sections are additions under the method's own headings, with fallback section by section: a missing file or heading means that part runs on its generic rule, never an error. Rejected: a mandatory heading set — a repository could not adopt the file one section at a time.
- `REVIEW.md` is read from the repository the command runs in, never the pull request's worktree, and CI restores `main`'s copy, removing the pull request's where `main` has none. Rejected: reading the checkout's copy — a pull request could weaken the rules it is reviewed against. Rejected: relying on the action's `.claude/` restore — it covers `CLAUDE.md` and no other root file.
- `REVIEW.md` always needs a person, stated in the generic command. Rejected: listing it in the file's own *Always a person* — a file that guards itself can be edited to stop, and a run by hand on the editing branch would honour the edit.
- The generic rules stay in the command for now and NetPace's file holds additions only, so the later extraction lifts the method without splitting the file.

**Date:** 2026-10-31
