# Commands read their conventions from CONVENTIONS.md

**Intent:** Take the NetPace specifics out of the SDLC commands and into one file at the repository root, read section by section, so the commands can later be lifted into a portable plugin whose defaults a repository extends by writing down only what is particular to it. Refs #478.

**Supersedes:** [`2026-10-20-harness-commands-keep-specifics-in-one-section`](2026-10-20-harness-commands-keep-specifics-in-one-section.md) — its decision that each command ends with a *Project-specific* section. The line it drew between repository-bound and harness-bound text still holds: the verdict protocol, the chain scripts and the sibling command names stay in the steps.

**Behaviour:** [`CONVENTIONS.md`](../../CONVENTIONS.md), read as each command's *Repository conventions* table says, on the pattern [`REVIEW.md`](../../REVIEW.md) set: sections under headings the commands document, each an addition to the command's generic rule, the file winning on conflict, and a missing file or heading meaning the generic rule alone — or a stop, where the convention is the thing itself and there is no generic rule. `CLAUDE.md` imports the file, so every session reads it, and keeps only the product patterns the file points back to.

**Constraints:** No command changes what it does on NetPace. References to the constitution, hooks and scripts left inside steps stay until the constitution is retired. `/install-harness-tooling` applies only to NetPace and is untouched.

**Decisions:**

- The heading set is the concerns the commands use today, with a section each for testing, TDD, acceptance criteria, code quality, dependencies and commit messages, so the constitution's rules move into existing sections when it is retired. Rejected: one section per command — several commands read the same convention, and a per-command file would hold it twice.
- The content `CLAUDE.md` kept about building, testing and documenting moves into the file, and `CLAUDE.md` imports it with `@`, as it imports the constitution. Rejected: leaving `CLAUDE.md`'s copy — two copies of the build command drift. Rejected: a link instead of an import — a session would no longer see the rules without opening the file.
- `/review-followup`'s one binding, its CI trigger, is a review criterion already stated in `REVIEW.md` → *Where it runs*, so the command points there and reads nothing from `CONVENTIONS.md`; `/verify`'s Review B binding points there for the same reason. Rejected: repeating the trigger under a `CONVENTIONS.md` heading — one fact in two files.
- In CI, `CONVENTIONS.md` is read from the same ref as the command file. `reviewissue.yml` checks out the default branch on its label path, so the command and its checks come from `main` together; no workflow runs `/build`, `/verify`, `/raise-pr` or `/draftissue`. The review workflows restore `.claude/` and `CLAUDE.md` from the base branch but not what `CLAUDE.md` imports, so their `REVIEW.md` step now puts `main`'s `CONVENTIONS.md` in place too; the review still sees a pull request's change to it in the diff. Rejected: leaving the checkout's copy — `main`'s `CLAUDE.md` would load the pull request's conventions as the reviewer's standing instructions.
- `CONVENTIONS.md` always needs a person, stated in `/review-pr`'s generic *Always a person* list beside `REVIEW.md`, for the reason the earlier record gives: a file that guards itself can be edited to stop.
- `/draftissue`'s concerns and `/reviewissue`'s checks stay as the questions each command asks, pointing at the concern section that holds the rule, so a rule is written once and a command's checklist stays readable in one place.
- `/audit-codebase`'s lens briefs keep their method and hand their repository knowledge — the constitution's principles and what to check under each, the evidence commands, the known flaky test, the generator-consumed symbols — to `CONVENTIONS.md` → *Audit*. Rejected: leaving the command as it was because it is not in the `/build` chain — it held more NetPace text than any other command, and every edit to it was adding more.
- `CLAUDE.md`'s coverage command was not carried over: the test projects reference no collector, so it collected nothing, as `CONVENTIONS.md` → *Audit* says.

**Date:** 2026-10-31
