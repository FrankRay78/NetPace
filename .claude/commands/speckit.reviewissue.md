# speckit.reviewissue

Review a GitHub issue before taking it into SDD — identify gaps, clarifications, and context the spec author will need, then post the review as a comment on the issue for inline answering. Re-runs of this command **edit the same comment in place** to expand any question where the author asked for more options or said "not sure" — substantive answers are left untouched for `/speckit.confirmissue` to fold into the issue body.

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

Look for an existing review comment marked with the sentinel `<!-- speckit:review -->`.
If multiple comments carry the marker, use the most recent one by ID.

- **No existing review comment** → **first run**. Continue to step 2 (full gap analysis, post a new comment).
- **Existing review comment found** → **refine run**. Skip to step 6 (re-frame hedging questions only; do not re-do gap analysis).

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

Group findings into two gap sections — **Requirements gaps** and **Technical gaps** — plus a **Notes for SDD** section for non-question observations.

Each gap (in either group) must:

- be answerable with a short written response (not "go figure it out")
- cite concrete evidence from the issue or codebase where relevant
- end with a **Recommendation:** line — your best judgement call with a
  one-sentence *Reason*. This is mandatory, not optional. The author should
  be able to read the recommendation and either accept it (record a short
  affirmative answer) or redirect it (record their chosen alternative). A
  gap without a recommendation forces the author to originate the answer
  from scratch, which is exactly the work this command is meant to front-load.

**Number gaps contiguously across both groups** (1, 2, 3, … not 1a, 1b). This keeps `/speckit.confirmissue` parsing simple and lets the author refer to questions by a single number in chat.

**Requirements gaps** — probe these before any technical question. Cover at least these categories when applicable:

- **User & persona** — who uses this, in what context. The issue may name a feature without naming the person.
- **Job-to-be-done** — the user-visible outcome that means "done"; contradictions between the stated outcome and the proposed mechanism.
- **Scenarios** — the 1–3 user-action / system-response flows the feature must support; missing edge scenarios (empty state, error states from the user's POV).
- **Scope & constraints** — contradictions between stated scope and available test data / reality (e.g. "issue says X-only, but test data is mostly Y"); items in Acceptance Criteria that are project-housekeeping rather than user-observable.
- **Semantics of user-visible behaviour** — matching rules, comparison scope, case/whitespace handling, what the user sees at boundaries.
- **Acceptance criteria from outside** — whether existing ACs are observable from outside the implementation by a user or external test; flag project-housekeeping items (project exists, sln updated, test scaffolding) — they belong in `/speckit.tasks`.
- **User-visible failure modes** — what the user sees when a dependency is unreachable, slow, or rejects them; fail-open vs fail-closed *from the user's viewpoint*.

**Technical gaps** — surface gaps suggested by Step 2 (codebase grounding) plus any tech shape the issue itself already commits to. Let the author set the depth: they may want extensive tech review or none. Items the author wants tracked but not answered now belong in **Notes for SDD**.

- **Where it lives** — existing endpoint/service/flow extended vs. new; who calls whom; which org(s) are involved.
- **Integration** — interface contracts with existing components, event/data flow, ordering.
- **Data shape** — seeding strategy, source of truth, storage choice (match existing patterns unless there is a reason not to).
- **Thresholds & numeric criteria** — confirm exact values, whether they reuse existing constants, what happens at boundaries.
- **Security boundary** — how access is enforced between services; auth mechanism (current codebase may have none).
- **Operational** — ports, migrations, docker compose entries, deploy scripts.
- **Tech failure modes** — unreachable dependencies, rate limits, retry policy, fail-open vs fail-closed at the system level.

**Notes for SDD** — things the spec author should *know* but doesn't need to answer here. Two flavours land in this section: passive observations that will shape the spec, and items the author chose to defer to `/speckit.specify` rather than resolve at issue level. Both are written as bullets, not questions:

- Port allocation suggestions based on existing assignments
- Reuse opportunities (existing classes/modules the new work can share)
- Docker / compose files that will need entries
- Documentation files that will need updates (list them by path)
- Testing conventions (mirror existing test project structure)
- Project-specific authoring rules (e.g. acceptance scenario naming conventions
  from `CLAUDE.md`)

### 4. Draft the comment

Structure the comment body as follows. Each gap gets an inline answer slot
(`> _Answer:_`) so the issue author can respond beneath it in a single edit.

```markdown
## Pre-specification review — gaps & clarifications

<!-- speckit:review -->

Before taking this into SDD, the following points need answers. Please record responses inline.

> If an answer slot says `not sure`, `idk`, `tbd`, `more options`, `help me`, or similar hedge (anything that means "I want help, not a decision"), re-run `/speckit.reviewissue #N` and that question will be re-framed with extra options, a worked example, and a revised recommendation. Iterate as many times as you need.
>
> If a gap turns out to be **out of scope** for this issue, answer with `out of scope: <one-line reason>` — `/speckit.confirmissue` will record it as a redirect. There is no separate "defer" path: anything not in scope here belongs in a different issue, not parked on this one.
>
> When all answers are concrete, run `/speckit.confirmissue #N` to fold them into the issue body as **Confirmed decisions**.

### Requirements gaps

**1. <short title>**
<concrete framing of the gap, including any evidence from issue/codebase>
- <sub-question 1>
- <sub-question 2>

> _**Recommendation:**_ <your best judgement call>. Reason: <one sentence>.

> _Answer:_

**2. <short title>**
...

> _**Recommendation:**_ ... Reason: ...

> _Answer:_

### Technical gaps

> Omit this section entirely if no technical gaps were identified — do not emit an empty heading. Gap numbering continues from the Requirements section (3, 4, …), not restarting at 1.

**N. <short title>**
...

> _**Recommendation:**_ ... Reason: ...

> _Answer:_

### Notes for SDD

- **<category>**: <observation>
- ...
```

Keep each gap tight. If a gap has more than ~3 sub-bullets, consider whether
it is actually two gaps.

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
- **Do not touch any other gap.** Substantive, out-of-scope, and empty answers must come through byte-for-byte. The "Notes for SDD" section is also untouched.

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

**First run:**
- confirm the issue reviewed (number + title)
- state how many gaps + how many notes were raised
- return the comment URL

**Refine run:**
- confirm the issue refined (number + title)
- list the gap numbers that were re-framed (e.g. *re-framed Q3 and Q5*)
- if no gaps qualified for re-framing, say so explicitly and suggest the next step — either fill in remaining empty answers, or run `/speckit.confirmissue #N` if all answers are concrete
- return the comment URL

Do **not** restate the full review in chat — it lives on the issue.

---

## When NOT to use this command

- The issue is already well-specified and has been through `/speckit.clarify`.
- The user wants implementation, not specification prep — that is a different
  workflow entirely.
- There is no GitHub issue yet — use `/speckit.specify` directly from a
  description instead.
