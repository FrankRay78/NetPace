# speckit.reviewissue

Review a GitHub issue before taking it into SDD — identify gaps, clarifications, and context the spec author will need, then post the review as a comment on the issue for inline answering.

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

### 1. Fetch the issue

Use `gh issue view <number> --repo <owner/repo>` (infer owner/repo from the URL
if given, otherwise use the current repository's `origin`).

Capture: title, body, labels, any existing comments. If existing comments
already resolve a gap you would otherwise raise, do not raise it again.

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

Group findings into two sections:

**Core gaps / clarifications** — questions the spec author *must* answer before
`/speckit.specify` can produce a sound spec. Each gap must:

- be answerable with a short written response (not "go figure it out")
- cite concrete evidence from the issue or codebase where relevant
- cover at least these categories when applicable:
  - **Scope & constraints** — contradictions between stated scope and available
    test data / reality (e.g. "issue says X-only, but test data is mostly Y")
  - **Semantics** — matching rules, comparison scope, case/whitespace handling,
    tolerance for errors
  - **Thresholds & numeric criteria** — confirm exact values, whether they
    reuse existing constants, what happens at boundaries
  - **Integration** — which existing endpoint/service/flow is extended vs.
    new; who calls whom; which org(s) are involved
  - **Data** — seeding strategy, source of truth, storage choice (match
    existing patterns unless there is a reason not to)
  - **Security boundary** — how access is enforced between services; auth
    mechanism (current codebase may have none)
  - **Failure modes** — unreachable dependencies, rate limits, retry policy,
    fail-open vs fail-closed
  - **Operational** — ports, migrations, docker compose entries, deploy scripts

**Notes for SDD** — things the spec author should *know* but don't need to
answer. These are observations that will shape the spec without being open
questions:

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

Before taking this into SDD, the following points need answers. Please record responses inline.

### Core gaps / clarifications

**1. <short title>**
<concrete framing of the gap, including any evidence from issue/codebase>
- <sub-question 1>
- <sub-question 2>
> _Answer:_

**2. <short title>**
...
> _Answer:_

### Notes for SDD

- **<category>**: <observation>
- ...
```

Keep each gap tight. If a gap has more than ~3 sub-bullets, consider whether
it is actually two gaps.

### 5. Post the comment

Post via `gh issue comment <number> --repo <owner/repo> --body "$(cat <<'EOF' ... EOF)"`.

**Escaping rules** — the heredoc body will contain backticks for inline code
(paths, class names, file names). To avoid shell interpretation issues:

- Use `'EOF'` (quoted) as the heredoc delimiter — this disables shell
  expansion inside the body, so backticks, `$`, and `\` are all passed through
  literally.
- Never use unquoted `EOF` here — it will try to expand `$variable` references
  and break on backticks.

After posting, return the comment URL to the user.

### 6. Do not modify the issue body

Your role is to comment, not edit. The issue author answers inline in the
comment you posted (or in a follow-up), and then decides when the issue is
ready for `/speckit.specify`.

---

## Output to the user

Keep your own chat response short:

- confirm the issue reviewed (number + title)
- state how many gaps + how many notes were raised
- return the comment URL

Do **not** restate the full review in chat — it lives on the issue.

---

## When NOT to use this command

- The issue is already well-specified and has been through `/speckit.clarify`.
- The user wants implementation, not specification prep — that is a different
  workflow entirely.
- There is no GitHub issue yet — use `/speckit.specify` directly from a
  description instead.
