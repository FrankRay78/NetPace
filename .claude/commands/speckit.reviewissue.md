# speckit.reviewissue

Review a GitHub issue before taking it into SDD — identify gaps, clarifications, and context the spec author will need, then post the review as a comment on the issue for inline answering. Re-runs of this command **edit the same comment in place** to expand any question where the author asked for more options or said "not sure" — substantive answers are left untouched for `/speckit.confirmissue` to fold into the issue body, which deletes the comment once they are folded.

---

## User Input

```text
$ARGUMENTS
```

`$ARGUMENTS` is expected to be either:
- a GitHub issue number (e.g. `19`), or
- a full GitHub issue URL (e.g. `https://github.com/OWNER/REPO/issues/19`).

You **MUST** resolve this before proceeding. If empty, ask the user which issue to review.

---

## Purpose

This command is a **pre-specification gate**. It sits *before* `/speckit.specify`.

Its job is to read an unrefined GitHub issue, cross-reference it against the
current codebase (architecture, existing services, test data, docs), and surface
everything that would otherwise block or distort a specification run:

- ambiguities in scope
- undefined semantics (matching rules, thresholds, field lists)
- integration points with existing components
- failure modes not covered
- inconsistencies between the issue text and reality on disk
- missing non-functional requirements (auth, rate limits, data seeding)

The output is a **single GitHub comment** posted on the issue, structured so the
issue author can record answers inline beneath each question.

---

## Workflow

### 1. Fetch the issue and detect mode

Use `gh issue view <number> --repo <owner/repo> --json title,body,labels,comments`
(infer owner/repo from the URL if given, otherwise use the current repository's
`origin`).

Check two things, in this order.

**First, is the issue already confirmed?** `/speckit.confirmissue` deletes the review once it has folded the answers into the issue body, so the review's absence no longer means "never reviewed". The `ready` label is what says the issue has been through this gate.

- **The issue carries the `ready` label** → **already confirmed**. **Stop.** Post nothing, edit nothing, and tell the user the issue has already been through the pre-specification gate, pointing them at its `## Confirmed decisions` section — or, if there is no such section, say so plainly: the label was applied by hand, and removing it is the fix. Say how to reopen it: remove the `ready` label (`gh issue edit <number> --repo <owner/repo> --remove-label ready`) and request the review again — an issue whose scope has moved is by definition no longer ready, and with the marker gone this command treats it as a first run. Never post a second review over settled decisions: it buries the `## Confirmed decisions` section under exactly the deliberation that confirming it was meant to clear away.

**Otherwise, look for an existing review comment** marked with the sentinel `<!-- speckit:review -->`. If multiple comments carry the marker, use the most recent one by creation time.

- **No existing review comment** → **first run**. Continue to step 2 (full gap analysis, post a new comment).
- **Existing review comment found** → **refine run**. Skip to step 6 (re-frame hedging questions only; do not re-do gap analysis).

Issues confirmed before the review was deleted on confirmation still carry their old review comment: the `ready` check catches them first, and the sentinel catches them if the label was never applied.

If existing non-review comments already resolve a gap you would otherwise raise,
do not raise it again.

### 2. Ground the review in the codebase

Before drafting gaps, read enough of the codebase to make the review *specific*
rather than generic. At minimum:

- `CLAUDE.md` and any documents it links (architecture, cheatsheet, testing conventions)
- The source tree area the issue is adding to or changing
- Any existing service / module that the new work would integrate with
- Test data, fixtures, and seed scripts referenced (directly or by implication) in the issue
- Deployment/docker config if the issue touches runtime shape

If the issue names a path (e.g. `src/GovernmentIdentityService/`), check whether
that path already exists and what conventions its siblings follow.

### 3. Identify gaps

Group findings into two gap sections — **Requirements gaps** and **Technical gaps** — plus a **Commentary** section.

Each gap (in either group) must:

- be answerable with a short written response (not "go figure it out")
- cite concrete evidence from the issue or codebase where relevant
- state its **consequence** — which of the two kinds below it is, and in one clause what a different answer would change
- end with a **Recommendation:** line — your best judgement call with a
  one-sentence *Reason*. This is mandatory, not optional. The author should
  be able to read the recommendation and either accept it (record a short
  affirmative answer) or redirect it (record their chosen alternative). A
  gap without a recommendation forces the author to originate the answer
  from scratch, which is exactly the work this command is meant to front-load.

**Raise a gap only where the issue leaves something open.** A gap exists because the issue does not determine the answer — someone taking this into SDD would have to invent it or guess. Test every candidate against the issue as it stands: if the body, its acceptance criteria, its out-of-scope list, or an existing non-review comment (step 1) already settles the point, there is no gap, however squarely a category below invites one. A category you considered and found settled produces **nothing** — no gap, no placeholder, and no commentary bullet announcing that it is fine.

Do **not** additionally ask whether the author would contest your recommendation — that is a judgement about a person rather than about the issue, and a wrong call settles a real decision silently. Every open point stays a numbered gap with its own answer slot, however confident the recommendation is.

**The review is sized by what the issue leaves open, not by the number of categories below.** The category lists are a checklist of what to *consider*, never a quota to fill. An issue that commits to little should attract a visibly shorter review than one that leaves much open — so if a tightly-scoped issue is producing a long review, the sweep is likely manufacturing gaps; re-test each against the issue and drop the ones nothing is actually open in.

**Consequence — every gap says what a different answer would change.** Each gap is exactly one of two kinds:

- **Changes what gets built** — answering against the recommendation would change something the issue commits to building: a scenario, an acceptance criterion, the scope boundary, a user-visible behaviour.
- **Settles a detail** — what gets built is fixed either way, and the answer settles a detail within it: a name, a path, a value, where something is documented.

Name the kind on the gap and say in one clause what would change. Where the call is genuinely borderline, take *Settles a detail* — an inflated marker costs the reader exactly the attention the marker exists to save.

**Order the gaps, then number them.** Settle the order first; numbers are assigned to the ordered list, never the reverse:

1. **By group** — all Requirements gaps, then all Technical gaps, because requirements are probed before any technical question. What is load-bearing here is the *grouping*, not the sequence: `/speckit.confirmissue` routes each folded decision by the heading its gap sat under (its step 4), so consequence ordering operates *within* a group and never moves a gap across the two.
2. **By consequence within the group** — every *Changes what gets built* gap comes before every *Settles a detail* gap in the same group.
3. **Number contiguously across both groups** (1, 2, 3, … not 1a, 1b), following that order. Contiguous numbering lets the author refer to a gap by a single number in chat.

Numbers are assigned once, when the comment is first composed. A refine run never reorders and never renumbers — see step 6.

**Requirements gaps** — probe these before any technical question. Consider each category below and raise a gap only where the issue leaves it open:

- **User & persona** — who uses this, in what context. The issue may name a feature without naming the person.
- **Job-to-be-done** — the user-visible outcome that means "done"; contradictions between the stated outcome and the proposed mechanism.
- **Scenarios** — the 1–3 user-action / system-response flows the feature must support; missing edge scenarios (empty state, error states from the user's POV).
- **Scope & constraints** — contradictions between stated scope and available test data / reality (e.g. "issue says X-only, but test data is mostly Y"); items in Acceptance Criteria that are project-housekeeping rather than user-observable.
- **Semantics of user-visible behaviour** — matching rules, comparison scope, case/whitespace handling, what the user sees at boundaries.
- **Acceptance criteria from outside** — whether existing ACs are observable from outside the implementation by a user or external test; flag project-housekeeping items (project exists, sln updated, test scaffolding) — they belong in `/speckit.tasks`.
- **User-visible failure modes** — what the user sees when a dependency is unreachable, slow, or rejects them; fail-open vs fail-closed *from the user's viewpoint*.

**Technical gaps** — surface gaps suggested by Step 2 (codebase grounding) plus any tech shape the issue itself already commits to. Let the author set the depth: they may want extensive tech review or none. Same test as above — consider each category, raise a gap only where something is genuinely unsettled.

- **Where it lives** — existing endpoint/service/flow extended vs. new; who calls whom; which org(s) are involved.
- **Integration** — interface contracts with existing components, event/data flow, ordering.
- **Data shape** — seeding strategy, source of truth, storage choice (match existing patterns unless there is a reason not to).
- **Thresholds & numeric criteria** — confirm exact values, whether they reuse existing constants, what happens at boundaries.
- **Security boundary** — how access is enforced between services; auth mechanism (current codebase may have none).
- **Operational** — ports, migrations, docker compose entries, deploy scripts.
- **Tech failure modes** — unreachable dependencies, rate limits, retry policy, fail-open vs fail-closed at the system level.

**Commentary** — remarks that inform the author reading this review and require no answer. That is the only kind of content the section carries. No downstream command parses it: `/speckit.confirmissue` folds only the numbered gaps into the issue body, and deletes this comment — commentary included — once it has. Write it for the person who reads the review, not for someone arriving at the issue afterwards.

**Commentary or gap?** The line is the *source* of the constraint, not its force. A fact already true of the codebase is commentary, however binding it turns out to be in practice. A choice only the author can make, or an obligation this issue would newly impose, is never commentary — record it as a numbered gap. A convention that already governs this area stays commentary even when this issue is the first work to trigger it; only an obligation with no prior basis in the codebase is a gap.

**The bar a bullet must clear.** Commentary carries only what that reader would otherwise miss or get wrong. A bullet that restates a rule they already hold — the constitution, `CLAUDE.md`, a guide either one links — or whose content amounts to "nothing to do here", does not appear at all. A short section is the normal outcome.

Write what survives as bullets, not questions:

- Local conventions that no gate enforces and no project guide states (e.g. a frontmatter key every sibling file sets that nothing checks)
- Port allocation suggestions based on existing assignments
- Reuse opportunities (existing classes/modules the new work can share)
- Docker / compose files that will need entries
- Documentation files that cover this area, listed by path

### 4. Draft the comment

Structure the comment body as follows. A table lists every gap up front so the author can triage the review before reading into it, and each gap then gets an inline answer slot (`> _Answer:_`) so they can respond beneath it in a single edit.

```markdown
## Pre-specification review — gaps & clarifications

<!-- speckit:review -->

Before taking this into SDD, the following points need answers. The table lists every gap and what turns on it — use it to decide where to spend your attention, then record responses inline beneath each gap.

> If an answer slot says `not sure`, `idk`, `tbd`, `more options`, `help me`, or similar hedge (anything that means "I want help, not a decision"), re-run `/speckit.reviewissue #N` and that question will be re-framed with extra options, a worked example, and a revised recommendation. Iterate as many times as you need.
>
> If a gap turns out to be **out of scope** for this issue, answer with `out of scope: <one-line reason>` — `/speckit.confirmissue` will record it as a redirect. There is no separate "defer" path: anything not in scope here belongs in a different issue, not parked on this one.
>
> When all answers are concrete, run `/speckit.confirmissue #N` to fold them into the issue body as **Confirmed decisions**. That deletes this comment — the decisions are the record from then on, and you revise one by editing its bullet.

### Gaps at a glance

| # | Gap | If answered against the recommendation |
|---|---|---|
| 1 | <short title> | Changes what gets built — <what would change, ≤12 words> |
| 2 | <short title> | Settles a detail — <what would change, ≤12 words> |
| 3 | <short title> | Changes what gets built — <what would change, ≤12 words> |

### Requirements gaps

**1. <short title>**
_Changes what gets built:_ <what a different answer would change, one clause>
<concrete framing of the gap, including any evidence from issue/codebase>
- <sub-question 1>
- <sub-question 2>

> _**Recommendation:**_ <your best judgement call>. Reason: <one sentence>.

> _Answer:_

**2. <short title>**
_Settles a detail:_ <what a different answer would change, one clause>
...

> _**Recommendation:**_ ... Reason: ...

> _Answer:_

### Technical gaps

> Omit this section entirely if no technical gaps were identified — do not emit an empty heading. Gap numbering continues from the Requirements section (3, 4, …), not restarting at 1.

**N. <short title>**
_<consequence kind>:_ <what a different answer would change, one clause>
...

> _**Recommendation:**_ ... Reason: ...

> _Answer:_

### Commentary

> Omit this section entirely if nothing clears the bar in Step 3 — do not emit an empty heading.

- **<category>**: <observation>
- ...
```

**The at-a-glance table.** One row per gap, in the same order as the gaps themselves, spanning both groups — so the consequence column is not sorted globally: it restarts at *Changes what gets built* where the Technical group begins. Titles in the `Gap` column match each gap's own title verbatim, and the consequence cell is a compression of the gap's own consequence line, so a row and its gap are unmistakably the same thing and never say different ones. Write every `|` inside the table as `\|` — in the title cell, the consequence cell, and inside inline code spans too, because GitHub splits a row into columns on an unescaped pipe before it renders code, so `` `A | B` `` in a cell breaks the row while `` `A \| B` `` renders as `A | B`. A gap title stays free to contain `|`: its title cell differs from the gap only by that escape and still counts as verbatim. Escape nothing outside the table — gap bodies, recommendations and commentary are prose where `|` is harmless. Always emit the table, even for a single gap: the author should never have to check whether it is there.

**Never use the `**N. <title>**` form in the table, and never put a `> _Answer:_` line above the first group.** `/speckit.confirmissue` parses every `**N. <title>**` block in the comment as a gap, ending at its `> _Answer:_` line (its step 2). A row imitating that shape carries no answer slot of its own, so it either hard-stops the fold — step 2 refuses to fold anything while a parsed gap looks unanswered — or takes the first real gap's answer slot as its own and corrupts the decisions that do land. Table cells carry a bare number and plain text, which matches nothing the parser looks for.

**Length bound — 120 words per gap.** Count everything from the `**N. <title>**` line through to its `> _Answer:_` slot: the consequence line, the framing, every sub-bullet, and the recommendation with its reason. Count whitespace-separated words of the prose, taking a markdown link as its link text rather than its URL. The bound applies to the gap as a whole rather than to any one part of it, and to the comment as first composed — a refine run's expansion (step 6) may exceed it, where keeping the re-framing tight is the goal rather than the ceiling.

**Splitting is not how you meet the bound.** A 200-word gap broken into two 100-word gaps satisfies nothing — the reader faces the same prose and one more decision. Cut instead: drop the restatement of what the issue already says, keep the evidence that makes the gap specific, and let the recommendation carry the detail rather than the framing. Split only where the gap is genuinely two independent questions needing two separate answers — and then each half must meet the bound on its own.

**Check the draft before posting.** Composing to a bound is not the same as meeting one — count, do not estimate. Before step 5 posts, verify against the draft: one table row per gap, its title matching the gap verbatim and its consequence cell agreeing with the gap's consequence line; no unescaped `|` anywhere in the table's content, inside inline code included, so every row renders as exactly three columns; every gap carrying a consequence line, a recommendation with its one-sentence reason, and an answer slot; every gap within the bound, counted rather than judged; *Changes what gets built* ahead of *Settles a detail* within each group; and no line above the first group heading matching either `**N. <title>**` or `> _Answer:_`. Fix what fails and re-check. This applies again to a refine run's edit (step 6), minus the bound.

**Recommendation quality bar:** the recommendation must be a concrete,
actionable default (a value, a library, a field name, an HTTP status, an
"in/out of scope" call) — not a meta-suggestion like "consider X". If you
genuinely have no view, say so explicitly and list the options with their
trade-offs; don't fake confidence. The *Reason* cites the evidence that led
you there (existing convention in the codebase, Fabric/framework behaviour,
POC posture, etc.), not a restatement of the recommendation.

**Link rules** — the comment is rendered on `https://github.com/<owner>/<repo>/issues/<N>`,
so GitHub resolves relative paths against the *issue URL*, not the repo root
(`[foo](src/Foo.cs)` becomes `…/issues/src/Foo.cs` — broken). Every link to a
file, directory, or line range **must** be an absolute GitHub URL:

- File: `https://github.com/<owner>/<repo>/blob/<default-branch>/<path>`
- File with line: append `#L<line>` or `#L<start>-L<end>`
- Directory: `https://github.com/<owner>/<repo>/tree/<default-branch>/<path>`

Resolve `<owner>/<repo>` from the issue (already known from step 1) and
`<default-branch>` via `gh repo view <owner>/<repo> --json defaultBranchRef --jq .defaultBranchRef.name`
once at the start of step 4 — reuse the result for every link in the body.
The link *text* can stay short (e.g. `[Program.cs:232-233](https://github.com/owner/repo/blob/main/src/NetPace.Console/Program.cs#L232-L233)`) so readability is unaffected.

This rule applies equally to refine-run edits in step 6 — any new links added
during re-framing must use the same absolute form.

### 5. Post the comment (first run only)

Post via `gh issue comment <number> --repo <owner/repo> --body "$(cat <<'EOF' ... EOF)"`.

**Escaping rules** — the heredoc body will contain backticks for inline code
(paths, class names, file names). To avoid shell interpretation issues:

- Use `'EOF'` (quoted) as the heredoc delimiter — this disables shell
  expansion inside the body, so backticks, `$`, and `\` are all passed through
  literally.
- Never use unquoted `EOF` here — it will try to expand `$variable` references
  and break on backticks.

After posting, return the comment URL and stop. Re-runs are handled by step 6.

### 6. Refine an existing review comment (re-runs)

When step 1 detects an existing comment with `<!-- speckit:review -->`, **do not
post a new comment** and **do not re-do gap analysis**. The downstream
`/speckit.confirmissue` command depends on every numbered gap (with its
`**Recommendation:**` and `> _Answer:_` lines) staying in the comment until
it folds them into the issue body. So this step is intentionally narrow:
its only job is to expand questions where the author asked for help.

Fetch the comment body verbatim (e.g. `gh api repos/<owner>/<repo>/issues/comments/<id>`
or via the comments JSON from step 1) and walk each numbered gap. For each:

| Answer state | Heuristic | Action |
|---|---|---|
| **Substantive** | A concrete decision (value, yes/no, chosen option, explicit "use the recommendation"). | **Leave untouched.** `/speckit.confirmissue` will fold it into the issue body. |
| **Out of scope** | Author's answer starts with `out of scope` (or `not for this issue`, `out of scope: <reason>`, etc.). | **Leave untouched.** `/speckit.confirmissue` records this as a Pattern C redirect. There is no separate "defer" path. |
| **Empty** | `> _Answer:_` slot is blank. | **Leave untouched.** The author hasn't tried to answer yet — re-framing now would just be noise. |
| **Hedging / asking for help** | Author's answer matches any hedging token (case-insensitive substring): `not sure`, `unsure`, `i'm not sure`, `dunno`, `idk`, `i don't know`, `???`, lone `?`, `help me`, `help`, `more options`, `more details`, `more detail`, `give me options`, `unclear`, `tbd`, `to be decided`. This list is the canonical dictionary — `/speckit.confirmissue` uses the same one to gate the body update. | **Re-frame this gap in place.** See rules below. |

Re-framing rules (hedging case only):

- **Preserve the gap number.** Q3 stays Q3 — never renumber.
- **Preserve the author's answer text** verbatim under the `> _Answer:_` line so they can see what they wrote last time.
- **Expand the question body** with 2–4 concrete options laid out as a sub-list, each with a one-line trade-off. Add a worked example or a pointer to a comparable existing pattern in the codebase (read the codebase again if needed — surface defaults they may not have known existed: existing constants, sibling service patterns, port allocations, etc.).
- **Revise the `> _**Recommendation:**_` line** if the new framing changes your call. Keep the `Reason:` cite tied to evidence.
- **After ~2 hedging iterations on the same question** with no commitment, add a final option *"This may be out of scope for the current issue — answer `out of scope: <reason>` to drop it"* and call it out in the recommendation. Do not edit the gap out yourself — leave that to the author + `/speckit.confirmissue`.
- **Keep the gap's consequence line and its table row in step with the re-framing.** If the new framing changes that gap's title or its consequence, update both places that state it — the `_<kind>:_` line on the gap body, and its row's title and consequence cells — and nothing else in the table. Never add, remove, reorder or renumber rows: the table mirrors the posted gap order, fixed when the comment was first composed. A consequence that changes after posting can therefore leave a *Changes what gets built* gap sitting below a *Settles a detail* one; that is the accepted cost of never renumbering a review the author already refers to by number.
- **Do not retrofit the table onto an older comment.** A comment posted before the at-a-glance table and the consequence line existed has no row to update and no kind to restate — leave it that way. A refine run re-frames the hedging gap and nothing else; it never adds a table to a comment that has none.
- **Do not touch any other gap.** Substantive, out-of-scope, and empty answers must come through byte-for-byte, and so must every table row but the one you changed. The Commentary section is also untouched.

If no gap qualifies for re-framing, **make no edit** and report that in chat (the author either still has un-answered questions, or is ready for `/speckit.confirmissue`).

**How to write the edit:**

Write the full updated comment body to `.claude/scratch/speckit-reviewissue-body.md` with the **Write tool** (never via shell heredoc — it'll bite you on backticks). Run `mkdir -p .claude/scratch` first if the directory does not yet exist (it is git-ignored). Then:

```bash
gh api -X PATCH repos/<owner>/<repo>/issues/comments/<id> \
  -F body=@.claude/scratch/speckit-reviewissue-body.md
```

(Omit the leading `/` on the endpoint — Git Bash on Windows rewrites `/repos/...`
as a filesystem path. `gh api` accepts both forms on Linux/macOS.)

### 7. Do not modify the issue body

Your role is to comment, not edit. The issue author answers inline in the
comment you posted (or in a follow-up), and then runs `/speckit.confirmissue`
when ready — that command is the one that touches the issue body.

---

## Output to the user

Keep your own chat response short. Tailor it to the run mode:

**Already confirmed (stopped at step 1):**
- say the issue has already been through the gate, and name the marker you saw (the `ready` label)
- point at its `## Confirmed decisions` section rather than restating it, or say if there is none
- state the reopen path: remove `ready`, then request the review again

**First run:**
- confirm the issue reviewed (number + title)
- state how many gaps were raised, split by consequence (e.g. *5 gaps — 2 change what gets built, 3 settle a detail*), and whether the review carries commentary
- return the comment URL

**Refine run:**
- confirm the issue refined (number + title)
- list the gap numbers that were re-framed (e.g. *re-framed Q3 and Q5*)
- if no gaps qualified for re-framing, say so explicitly and suggest the next step — either fill in remaining empty answers, or run `/speckit.confirmissue #N` if all answers are concrete
- return the comment URL

Do **not** restate the full review in chat — it lives on the issue.

---

## When NOT to use this command

- The issue has already been confirmed — it carries the `ready` label and a `## Confirmed decisions` section. Reopen it deliberately (step 1) if its scope has moved.
- The issue is already well-specified and has been through `/speckit.clarify`.
- The user wants implementation, not specification prep — that is a different
  workflow entirely.
- There is no GitHub issue yet — use `/speckit.specify` directly from a
  description instead.
