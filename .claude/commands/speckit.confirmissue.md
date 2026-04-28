# speckit.confirmissue

Take a `/speckit.reviewissue` comment that the author has answered inline, fold the answers into a clean **Confirmed decisions** bullet list, and append that list to the issue body so `/speckit.specify` consumes decisions, not deliberation.

---

## User Input

```text
$ARGUMENTS
```

`$ARGUMENTS` is expected to be either:
- a GitHub issue number (e.g. `25`), or
- a full GitHub issue URL (e.g. `https://github.com/OWNER/REPO/issues/25`).

You **MUST** resolve this before proceeding. If empty, ask the user which issue to confirm.

---

## Purpose

Sits **between** `/speckit.reviewissue` and `/speckit.specify`.

`/speckit.reviewissue` posts a comment of gaps + recommendations + empty answer slots. The author fills in the answers in the GitHub UI. This command then:

1. Reads the answered review comment.
2. Pairs each gap's `**Recommendation:**` with the author's `> _Answer:_` to produce a **one-line decision** per gap.
3. Appends (or rewrites, if already present) a `## Confirmed decisions` section at the end of the issue body.

The original review comment is left untouched — it serves as the natural audit trail of how each decision was reached.

The author also owns the issue body, so post-pending decisions to it is not an overstep. This is the moment the "do not modify the issue body" rule from `/speckit.reviewissue` lifts.

---

## Workflow

### 1. Resolve issue and locate the review comment

Use `gh issue view <number> --repo <owner/repo> --json body,comments` to fetch the issue body and full comment list (infer owner/repo from the URL or the current repo's `origin`).

Find the review comment by searching for the hidden marker `<!-- speckit:review -->`. If multiple comments carry the marker (e.g. the author re-ran `/speckit.reviewissue`), use the **most recent** one by ID.

If no marker comment exists, **stop** and tell the user the issue has no `/speckit.reviewissue` comment to confirm.

### 2. Verify all gaps are answered, and no answers are still hedging

Parse each `**N. <title>**` block in the comment. Each block ends with a `> _Answer:_` line. For each block, check whether the author has written a non-empty answer (either inline on the same line as `> _Answer:_`, or on the following non-blank line).

If **any** answer is empty, **stop** and report which gap numbers (and titles) are still unanswered. Do not modify the issue body.

Then, for each non-empty answer, check for **hedging** — text that means "I want help, not a decision". Treat any of the following as hedging (case-insensitive, substring match on a token-trimmed answer):

- `not sure`, `unsure`, `i'm not sure`, `dunno`, `idk`, `i don't know`
- `???`, `?` (when the answer is just a single `?`)
- `help me`, `help`, `more options`, `more details`, `more detail`, `give me options`
- `unclear`, `tbd`, `to be decided`

If **any** answer is hedging, **stop** and report which gap numbers are still hedging. Tell the author to re-run `/speckit.reviewissue #N` to expand those questions, then come back. Do not modify the issue body.

### 3. Pair recommendations with answers → one decision bullet per gap

For each gap, extract:
- the gap title (the bold text after the number)
- the `**Recommendation:**` content (everything between `**Recommendation:**` and the trailing `Reason:`)
- the author's answer text

Classify the answer into one of three patterns and write a single bullet accordingly. The **bullet is a decision**, not a Q&A — phrase it as the resolved outcome:

**Pattern A — Accepted as-is** (answer is "yes", "agreed", "as recommended", "ok", or similar affirmation with no rider):
```
- **<short title>:** <recommendation, lightly rephrased as a decision>.
```
Example — answer "Agreed" → `- **eventId generation:** Use \`ctx.GetStub().GetTxID()\` directly as \`eventId\`.`

**Pattern B — Accepted with rider** (answer agrees but adds a constraint, scope tweak, or extra detail):
```
- **<short title>:** <recommendation as a decision>. <rider folded in>.
```
Example — answer "Agreed, also surface in GET response" → `- **submittingOrg field:** Server-set from \`OrgConfig.Name\`; surfaced in GET response.`

**Pattern C — Redirected** (answer rejects the recommendation and proposes an alternative, or marks the gap as out of scope):

If the author *gave a reason*:
```
- **<short title>:** <author's chosen alternative as a decision>. (Author redirected from "<recommendation summary>" — <reason>.)
```
If the author *did not* give a reason, drop the dash and the reason — never emit `— .`:
```
- **<short title>:** <author's chosen alternative as a decision>. (Author redirected from "<recommendation summary>".)
```

Example — answer "No, use sha256(txID + bid) — don't leak Fabric internals" → `- **eventId generation:** Use \`sha256(txID + bid)\` as \`eventId\`. (Author redirected from raw txID to avoid leaking Fabric internals.)`

Example — answer "No, use sha256" (no reason) → `- **eventId generation:** Use \`sha256\` as \`eventId\`. (Author redirected from raw txID.)`

**Out-of-scope sub-case.** If the answer starts with `out of scope` (or `not for this issue`, `out of scope: <reason>`, etc.), phrase the bullet as a scope decision rather than inventing an alternative:
```
- **<short title>:** Out of scope for this issue<: reason if given>. (Author redirected from "<recommendation summary>".)
```
Example — answer "out of scope: caching belongs in a separate ticket" → `- **payload caching:** Out of scope for this issue: caching belongs in a separate ticket. (Author redirected from "add LRU cache".)`

Bullet titles should be a short phrase derived from the gap title — drop "will break", "needs", "vs", question-like fragments. Examples: "Non-deterministic eventId will break endorsement" → `eventId generation`; "Payload size limits" → `data cap`; "Scope — is Portal integration in or out?" → `Portal scope`.

Keep bullets to one line where possible. Two lines if the rider genuinely needs it. Never paragraphs — the spec author will read this as a checklist.

### 4. Build the new issue body

Take the existing issue body and:

- **If `## Confirmed decisions` already exists**: replace that section. The section runs from the `## Confirmed decisions` heading through to the next `## ` heading (any other H2) or end-of-file, whichever comes first.
- **If it does not exist**: append at end-of-body, except when the body ends with footer-style H2 sections (`## Related`, `## References`, `## Links`, `## See also`) — in that case insert immediately before the first such footer section. Separate by a blank line either way.

> **Section ownership.** The `## Confirmed decisions` section is owned by this command. Any human edits the author makes inside the section (annotations between bullets, extra prose, hand-tuned bullets) will be **clobbered** on re-runs. The `<!-- speckit:confirmed-decisions -->` marker is a tooling hint, not a fence — it does not protect the section. If the author wants to record extra context, they should put it in a different H2 section, or edit the source recommendations/answers in the review comment and re-run this command.

The new section is structured as:

```markdown
## Confirmed decisions

<!-- speckit:confirmed-decisions -->

- **<title>:** <decision>.
- **<title>:** <decision>.
- ...
```

Preserve everything above the section byte-for-byte. Do not "tidy" the original proposal.

### 5. Patch the issue body

Use the JSON-via-jq pattern to avoid any shell escaping pitfalls (backticks, `$`, etc.):

```bash
jq -n --rawfile b /tmp/speckit-confirmissue-body.md '{body: $b}' > /tmp/speckit-confirmissue-patch.json
gh api --method PATCH /repos/<owner>/<repo>/issues/<number> --input /tmp/speckit-confirmissue-patch.json --jq .html_url
```

Write the new body to a temp file with the **Write tool**, never via shell heredoc.

### 6. Do not touch the review comment

The answered review comment is the audit trail. Leave it intact. Do not delete, edit, or annotate it.

---

## Output to the user

Keep your chat response short:

- confirm the issue updated (number + title)
- state how many decisions were folded in (and the count by pattern: e.g. "8 accepted, 1 with rider, 1 redirected")
- return the issue URL

If you stopped at step 1 or 2, report which precondition failed and what to fix.

Do **not** restate the decisions list in chat — it lives on the issue.

---

## When NOT to use this command

- The issue has no `/speckit.reviewissue` comment yet — run that first.
- The review comment exists but has empty `> _Answer:_` slots — fill them in first.
- The issue body already has a `## Confirmed decisions` section AND the author has not changed any answers since — re-running is harmless (idempotent) but unnecessary.
- The user wants to skip review and go straight to spec — that is `/speckit.specify` directly, no decision log needed.
