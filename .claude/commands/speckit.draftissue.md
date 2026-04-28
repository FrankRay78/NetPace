# speckit.draftissue

Turn an unstructured feature brief into a well-formed GitHub issue, ready for `/speckit.reviewissue` and then `/speckit.specify`.

---

## User Input

```text
$ARGUMENTS
```

`$ARGUMENTS` is expected to be an **unstructured feature brief** — a paragraph or rough bullet list describing capability the user wants, possibly with mixed concerns.

If empty, ask the user for the brief. Do not invent one.

---

## Purpose

Pre-issue gate. Sits *before* `/speckit.reviewissue`, which sits before `/speckit.specify`.

Job: take a half-formed idea, surface the decisions the author hasn't made yet, work through them collaboratively, and emit a structured GitHub issue. The aim is that when `/speckit.reviewissue` later runs against the issue, it has substantive scope, acceptance criteria, and an explicit out-of-scope list to cross-check against the codebase.

Output: a GitHub issue created via `gh`. A transient draft file is used as the editing surface during iteration; it does not survive the run.

---

## Workflow

### 1. Capture the brief

Read `$ARGUMENTS`. If it is empty or a single sentence, ask the user to expand. Do not proceed with a thin brief — the value of this command is in the structured conversation, which needs raw material.

### 2. Ground the draft in the codebase

Before asking the user any questions, explore the codebase so questions are *informed*. Spawn an Explore subagent (`subagent_type=Explore`, medium-thorough) and ask it for a structured report. The exact contents depend on the project, but at minimum cover:

- **Domain primitives** the brief touches (whatever core types, identifiers, or schemas the project centres on) — cite file paths and line numbers.
- **Existing surfaces** the new work would attach to (services, modules, endpoints, data stores, UI routes — whichever apply).
- **Conventions** that bind the new work — find them via `CLAUDE.md`, `README.md`, or sibling code. Common locations: a testing doc, an architecture doc, a contributing guide.
- **Gotchas / constraints** — schema versioning, idempotency, latency budgets, coupling, anything that complicates the obvious approach.
- Existing concepts adjacent to the brief — if the brief mentions "events" / "auth" / "search" / etc., does the codebase already have something analogous? If so, contrast.

Tell the agent *not* to design the feature, only to surface what exists. Cap report length so it doesn't dominate context.

### 3. Surface decision points to the user

Identify the ~5–10 decisions the brief leaves open. For each, present:

- a one-line framing of the decision,
- the realistic options with their trade-offs,
- a **lean** — your concrete recommended default,
- where relevant, a **POC vs prod** split (what's acceptable now vs what to architect for later).

Each decision should be answerable with a short response. The user can rubber-stamp the lean or redirect.

**Recommendation quality bar:** identical to `/speckit.reviewissue` — concrete actionable defaults (a value, a library, a field name, a scope call), not "consider X". The reason cites evidence: existing convention, framework behaviour, POC posture, codebase constraint. If you genuinely have no view, say so and list options with trade-offs; don't fake confidence.

Common categories to probe (apply only those that fit the brief):

- **Data model & storage location** — where data lives, schema shape, mandatory vs optional fields, mutability.
- **Identity & auth** — who is the caller, how is identity carried/validated, what's enforced now vs later.
- **Read / query design** — what queries the feature must support, indexing strategy, pagination, sort order.
- **Schema / API evolution** — fixed enum vs free-form, versioning, upgrade path.
- **Notification / integration** — push vs pull, webhooks, downstream consumers, event ordering.
- **UI / state** — component placement, routing, state ownership, loading & error states (if the brief touches a frontend).
- **Public API surface** — request/response shape, status codes, error model, idempotency keys.
- **Performance budget** — expected throughput, latency targets, payload size bounds.
- **Scope edges** — what's explicitly *out* (logging? archival? admin UI? migration of existing data?).
- **Failure modes** — unknown inputs, missing references, partial success, retries.

### 4. Iterate to lock decisions

Capture answers. Where the user delegates back ("you advise", "what's best?"), give a single concrete recommendation with a one-sentence justification — do not re-open the menu. Confirm any small interpretation questions before drafting (e.g. mandatory sub-fields inside an optional struct).

### 5. Draft to a transient file

Pick a scratch location appropriate to the host OS — typically the system temp directory (`/tmp` on Linux / macOS / WSL, the OS temp dir on native Windows). Resolve it however is most reliable for the current shell; if you genuinely can't determine one, fall back to `~/.claude/` which exists wherever Claude Code is installed. Write the draft to `<scratch_dir>/gh-issue-body-{slug}.md` using the native `Write` tool, where `{slug}` is a short kebab-case derivation of the title. Tell the user the resolved absolute path so they can open it in their editor for review.

The file is the issue body verbatim — what we write is what we post. Write all internal links as **paths relative to the repository root** (e.g. `[architecture](docs/ARCHITECTURE.md)`, `[handler](src/api/handler.ts)`), which is how GitHub resolves links in issue bodies. Do **not** include an H1 title in the file — that goes on the `gh issue create --title` flag, not in the body.

Use this template:

```markdown
## Summary

One short paragraph stating what the feature delivers and the user-visible outcome.

## Motivation

Why now, what's missing today, who needs this. Cite concrete evidence from the codebase (file paths + line numbers) where relevant.

## Proposal

### <Component-level sub-sections as the work demands>
e.g. data model, transactions, endpoints, UI flow, auth, schema migration. Use whatever sub-sections the feature needs — there is no fixed list.

## Out of scope

Bullet list. Be explicit about what's deliberately deferred — this is the most useful section for `/speckit.reviewissue` to cross-check.

## Acceptance criteria

Checklist of testable outcomes. Each item must be observable from outside the implementation. Reference the project's test conventions where relevant.

## Open questions / future work

Bullets. Things that don't need to be answered to ship the feature but should be tracked.

## Related

- Links to architecture docs, testing conventions, and the source files most relevant to the work — paths relative to the repository root.
```

### 6. User review

Tell the user the file path and a short summary of what's in it. Wait for edits, push-back, or approval. Apply requested edits with `Edit` against the temp file. Do **not** raise the issue until the user explicitly says to.

The user can also edit the temp file directly in their editor. If they do, `Read` the file again before any further `Edit` calls so the in-context state matches disk.

### 7. Raise the issue and clean up

When the user approves, create the issue and delete the temp file in a single step, using the absolute path resolved in step 5:

```bash
gh issue create --title "<title>" --body-file <scratch_dir>/gh-issue-body-<slug>.md && rm <scratch_dir>/gh-issue-body-<slug>.md
```

Verify auth and target repo first with `gh auth status` and `gh repo view --json nameWithOwner` if you're not certain.

Return the issue URL.

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
