# speckit.draftissue

Turn an unstructured feature brief into a well-formed GitHub issue, ready for `/speckit.reviewissue` and then `/speckit.specify`.

---

## User Input

```text
$ARGUMENTS
```

`$ARGUMENTS` is expected to be an **unstructured feature brief** — a paragraph or rough bullet list describing capability the user wants, possibly with mixed concerns.

The brief MAY reference one or more existing GitHub issue URLs/numbers as input — e.g. *"I want to use an existing issue as input, that issue will be closed, can we start from there: <url>"*. Treat this as a **migration brief**: ingest the referenced issue(s) in Step 1, classify their content in Step 2.5, and propose closing them in Step 7 (always with explicit user confirmation).

If empty, ask the user for the brief. Do not invent one.

---

## Purpose

Pre-issue gate. Sits *before* `/speckit.reviewissue`, which sits before `/speckit.specify`.

Job: take a half-formed idea, surface the decisions the author hasn't made yet, work through them collaboratively, and emit a structured GitHub issue. The aim is that when `/speckit.reviewissue` later runs against the issue, it has substantive scope, acceptance criteria, and an explicit out-of-scope list to cross-check against the codebase.

Output: a GitHub issue created via `gh`. A transient draft file is used as the editing surface during iteration; it does not survive the run.

---

## Workflow

### 1. Capture the brief

Read `$ARGUMENTS`. If it is empty, ask the user to expand. Do not invent one.

**Issue-URL ingest (migration mode).** If the brief references one or more GitHub issue URLs or `#N` numbers as input, fetch each with `gh issue view <N> --repo <owner/repo> --json title,body,comments`. From each issue, treat the union of the issue body + any comment marked `<!-- speckit:review -->` + any `## Confirmed decisions` section as additional brief material. Tell the user which issues were ingested before proceeding, and flag this run as a migration so Step 2.5 and Step 7 behave accordingly. A short brief like "*use #44*" is sufficient in migration mode — the source issue body supplies the raw material.

Otherwise, if the brief is a single sentence with no migration reference, ask the user to expand. The value of this command is in the structured conversation, which needs raw material.

### 2. Ground the draft in the codebase

Before asking the user any questions, explore the codebase so questions are *informed*. Spawn an Explore subagent (`subagent_type=Explore`, medium-thorough) and ask it for a structured report. The exact contents depend on the project, but at minimum cover:

- **Domain primitives** the brief touches (whatever core types, identifiers, or schemas the project centres on) — cite file paths and line numbers.
- **Existing surfaces** the new work would attach to (services, modules, endpoints, data stores, UI routes — whichever apply).
- **Conventions** that bind the new work — find them via `CLAUDE.md`, `README.md`, or sibling code. Common locations: a testing doc, an architecture doc, a contributing guide.
- **Gotchas / constraints** — schema versioning, idempotency, latency budgets, coupling, anything that complicates the obvious approach.
- Existing concepts adjacent to the brief — if the brief mentions "events" / "auth" / "search" / etc., does the codebase already have something analogous? If so, contrast.

Tell the agent *not* to design the feature, only to surface what exists. Cap report length so it doesn't dominate context.

### 2.5. Classify existing content (migration runs only)

Skip this step if no existing issue was ingested in Step 1.

For each ingested issue, sort the content into two buckets and present the classification to the user **before** opening any Q&A:

- **Requirements candidates** — user/persona, jobs-to-be-done, observable behaviour, scope edges, acceptance criteria phrased as user-visible outcomes.
- **Technical-notes candidates** — file paths under `src/`, ports, library picks, integration shape, visual aesthetic anchors, exact UI copy, polling cadences, project-housekeeping ACs ("project X exists", "solution file includes Y").

Show the split as two short lists referencing the original headings/bullets, and ask the user to confirm or redirect items between buckets. Then proceed to Step 3.

### 3. Surface decision points to the user

Identify the ~5–10 decisions the brief leaves open. For each, present:

- a one-line framing of the decision,
- the realistic options with their trade-offs,
- a **lean** — your concrete recommended default,
- where relevant, a **POC vs prod** split (what's acceptable now vs what to architect for later).

Each decision should be answerable with a short response. The user can rubber-stamp the lean or redirect.

**Recommendation quality bar:** identical to `/speckit.reviewissue` — concrete actionable defaults (a value, a library, a field name, a scope call), not "consider X". The reason cites evidence: existing convention, framework behaviour, POC posture, codebase constraint. If you genuinely have no view, say so and list options with trade-offs; don't fake confidence.

Common categories to probe (apply only those that fit the brief). Walk **Requirements** categories first — they shape the issue body's main half. Then walk **Technical** categories — suggest the ones Step 2 surfaced from the codebase, and let the author set the depth.

**Requirements categories** (always probe these first):

- **User & persona** — who uses this feature, in what context, on what surface.
- **Job-to-be-done** — the task the user is trying to accomplish; the observable outcome that means "done" from their viewpoint.
- **Scenarios** — the 1–3 concrete flows the feature must support, phrased as user actions and observable system responses.
- **Scope edges** — what's explicitly *out*. Be precise: actual scope decisions (e.g. "no admin UI in v1", "no migration of pre-existing records"), not technical defaults restated negatively.
- **Semantics of user-visible behaviour** — matching rules, comparison scope, case/whitespace handling, what the user sees at boundaries.
- **Acceptance criteria from outside** — what an observer (not the implementer) can see and check. Phrase each AC as a **user-observable outcome**, not the implementation mechanism that delivers it. Multiple reasonable implementations should satisfy the same AC. This is **Principle IX (Behavioural Specification)** in `.specify/memory/constitution.md` — `/speckit.analyze` enforces it.

  *The independence test*: would this AC still be true under a different reasonable implementation of the same feature? If no, you've described the mechanism — re-phrase at the level the user actually cares about.

  | Avoid (mechanism, prescriptive) | Prefer (outcome, level-up) |
  |---|---|
  | "Row gains `.flash` class for ~800ms via CSS animation" | "Users can visually distinguish new rows from existing ones" |
  | "Sidebar entries appear in DOM order: Identities, Services, Events" | "Sidebar entries follow a fixed, predictable order" |
  | "On click, JS toggles `hidden` on screen sections" | "Switching screens shows the chosen content without page reload" |
  | "Error toast displays 'Invalid email address' in red" | "User is told the input was rejected and given guidance to correct it" |
  | "Endpoint returns `{status: 'ok', data: [...]}` with HTTP 200" | "Caller can retrieve the current list of services in a single request" |
  | "Setting is written to the `user_preferences` table and cached in Redis" | "User's setting is remembered across sessions and visible on next sign-in" |
  | "Endpoint returns `[]` when no matches found" | "User is shown a clear empty state when no results match" |

  *Do not write into ACs*: CSS class names, DOM IDs or element types, animation specifics, font names/weights/colours, framework or library picks, pixel measurements, timing values (Ns / Nms), polling cadences, specific URLs or ports, exact error message strings, storage technology. These are implementation choices, not acceptance criteria. Project-housekeeping items (project exists, sln updated, test scaffolding created) belong in `/speckit.tasks`, not here.

  *Regression exception*: an AC that pins a specific mechanism is permitted only when it exists to prevent a named, previously-fixed bug — reference the bug.
- **User-visible failure modes** — what the user sees when a dependency is unreachable, slow, or rejects them; what's communicated; what stays available.

**Technical categories** (suggest from Step 2 findings; the author sets the depth):

- **Where it lives** — which existing service/module the new work attaches to, or whether it's a new surface. Avoid locking exact paths/ports here unless the brief is explicit.
- **Integration points** — which existing endpoints, schemas, events, or services the work depends on or extends.
- **Data shape** — storage location, schema sketch, mutability — only at the level the brief already implies.
- **Constraints / gotchas** — idempotency, latency budgets, coupling, conventions binding the new work.
- **Open tech questions** — things the spec author will need to resolve in `/speckit.specify` or `/speckit.plan`.

If a candidate question doesn't fit either group, it probably belongs downstream — leave it for `/speckit.specify` rather than forcing it into the issue.

### 4. Iterate to lock decisions

Capture answers. Where the user delegates back ("you advise", "what's best?"), give a single concrete recommendation with a one-sentence justification — do not re-open the menu. Confirm any small interpretation questions before drafting (e.g. mandatory sub-fields inside an optional struct).

### 5. Draft to a transient file

Write the draft to `.claude/scratch/gh-issue-body-{slug}.md` using the native `Write` tool, where `{slug}` is a short kebab-case derivation of the title. The directory is git-ignored — run `mkdir -p .claude/scratch` first if you're not certain it exists yet. Tell the user the file path so they can open it in their editor for review.

The file is the issue body verbatim — what we write is what we post. Write all internal links as **paths relative to the repository root** (e.g. `[architecture](docs/ARCHITECTURE.md)`, `[handler](src/api/handler.ts)`), which is how GitHub resolves links in issue bodies. Do **not** include an H1 title in the file — that goes on the `gh issue create --title` flag, not in the body.

Use this template. The body is split by a horizontal rule (`---`) into a **Requirements** half (what we are building, user-observable) and an optional **Technical notes** half (current best thinking about how). Drop the entire `---` block — heading and all — if the author wants no tech-shape commitments in the issue.

```markdown
## Summary

One short paragraph stating what the feature delivers and the user-visible outcome.

## Motivation

Why now, what's missing today, who needs this. Cite concrete evidence from the codebase (file paths + line numbers) where relevant.

## Users & jobs

Who uses this feature, in what context, and the task they are trying to accomplish. One short paragraph or 2–4 bullets per persona — no more.

## Capability

What the feature *does* from outside, described as user-observable behaviour and the 1–3 scenarios that exercise it. Use whatever short sub-sections the feature needs. Do **not** specify ports, file paths under `src/`, polling cadences, framework picks, visual aesthetic anchors, or exact UI copy here — those are tech shape, not requirements. Defer them to the Technical notes section below or to `/speckit.specify`.

## Out of scope

Bullet list of deliberate scope decisions. Phrase as "X is not in this issue", not as technical defaults restated negatively.

## Acceptance criteria

Checklist of testable outcomes, **all observable from outside the implementation by a user or external test**. Project-housekeeping items (new project exists, sln updated, test scaffolding created) belong in `/speckit.tasks`, not here. Reference the project's test conventions where relevant.

---

## Technical notes

> Omit this entire `---`-separated block — heading and all — if no Technical notes content was captured. `/speckit.specify` and `/speckit.plan` are the canonical places to fix tech shape if the author wants to defer.

### Where it lives
### Integration points
### Constraints / assumptions
### Unknowns

Use the sub-headings that fit; drop the rest. Keep bullets, not paragraphs.

---

## Open questions / future work

Bullets. Things that don't need to be answered to ship the feature but should be tracked.

## Related

- Links to architecture docs, testing conventions, and the source files most relevant to the work — paths relative to the repository root.
```

### 6. User review

Tell the user the file path and a short summary of what's in it. Wait for edits, push-back, or approval. Apply requested edits with `Edit` against the temp file. Do **not** raise the issue until the user explicitly says to.

The user can also edit the temp file directly in their editor. If they do, `Read` the file again before any further `Edit` calls so the in-context state matches disk.

### 7. Raise the issue and clean up

When the user approves, create the issue and delete the scratch file in a single step:

```bash
gh issue create --title "<title>" --body-file .claude/scratch/gh-issue-body-<slug>.md && rm .claude/scratch/gh-issue-body-<slug>.md
```

Verify auth and target repo first with `gh auth status` and `gh repo view --json nameWithOwner` if you're not certain.

Return the issue URL.

**Migration mode — supersede the source issue(s).** If Step 1 ingested existing issues, after the new issue URL is known, **always confirm with the user before closing any source issue** — even if the brief said "close it". The brief was written before the new issue existed; the user may want to eyeball the new one first. For each source issue the user confirms for closure:

```bash
gh issue close <N> --comment "Superseded by #<M>"
```

Confirm per-issue, not in bulk.

### 8. Stop

Do not move on to `/speckit.specify` or implementation. The next step in the SDD workflow is `/speckit.reviewissue` against the new issue, which the user will trigger separately.

If the repo has no GitHub remote, stop after step 6, leave the temp file in place, and tell the user — they can post it manually if needed.

---

## Output to the user

Keep your own chat response short:

- confirm the issue raised (URL)
- highlight the key decisions that were locked, in one or two bullets

Do **not** restate the full draft in chat — the issue itself is now canonical.

---

## When NOT to use this command

- The user already has a structured issue body — go straight to `gh issue create` or to `/speckit.reviewissue`.
- The brief is for a bug fix or trivial change — overhead isn't justified; just write the issue inline.
- The user wants implementation, not issue authoring — wrong workflow.
- The repo has no GitHub remote — the raise step will fail. You can still draft to the temp file, but tell the user up front and stop after review.
