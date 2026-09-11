# speckit.confirmissue

Take a `/speckit.reviewissue` comment that the author has answered inline, fold the answers into a clean **Confirmed decisions** bullet list, append that list to the issue body so `/speckit.specify` consumes decisions, not deliberation — then delete the review comment it came from.

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
4. Applies the `ready` label, marking the issue fully defined without anyone having to open it.
5. Deletes the review comment, now that everything worth keeping from it is on the issue body.

The review comment is working material, not a record. A numbered gap carries framing, cited evidence, a recommendation with its reason, and an answer slot; ten of those compress to ten one-line decisions. Once that compression has happened the comment dwarfs the section that matters, and a reader arriving at a confirmed issue has to work out which of the two halves is current. The confirmed-decision bullets already carry each gap's title, its resolved outcome, and `(Author redirected from "…")` where the author overruled the recommendation — which is the causality anyone needs afterwards.

**The `ready` label is the done-marker.** It is what tells `/speckit.reviewissue` and the `speckit-reviewissue.yml` workflow that this issue has been through the gate, so neither posts a second review over decisions that were settled weeks earlier. That is why step 7 deletes the comment only once step 6's label is on the issue: the new marker must be in place before the old one is removed.

The author also owns the issue body, so post-pending decisions to it is not an overstep. This is the moment the "do not modify the issue body" rule from `/speckit.reviewissue` lifts.

---

## Workflow

### 1. Resolve issue and locate the review comment

Use `gh issue view <number> --repo <owner/repo> --json body,labels,comments` to fetch the issue body, its labels and the full comment list (infer owner/repo from the URL or the current repo's `origin`). The labels are needed here because `ready` — not the comment — is what says an issue has already been through the gate.

Find the review comment by searching for the hidden marker `<!-- speckit:review -->`. If multiple comments carry the marker (e.g. the author re-ran `/speckit.reviewissue`), use the **most recent** one by ID.

If no marker comment exists, distinguish two cases before stopping:

- **The issue body already carries a `## Confirmed decisions` section, or the issue carries the `ready` label** → it has already been confirmed, and its review was deleted at the end of that run. **Stop** and report that the issue is already confirmed, stating how many decisions are on the body. Do not report that there is no review to confirm — that reads as "never reviewed", which is the opposite of the truth. If the `ready` label is missing (a previous run saved the decisions but could not label), apply it here with step 6's rules, and say so.
- **Neither is present** → **stop** and tell the user the issue has no `/speckit.reviewissue` comment to confirm.

To put an already-confirmed issue back through review, see step 7.

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

> **Revising a confirmed decision.** A successful run deletes the review the decisions came from, so there is no upstream source left to edit and re-fold. **Edit the bullet in `## Confirmed decisions` directly** — that is the supported way to revise a decision, and this command no longer claims ownership it cannot exercise. The replace path above therefore only fires when a *new* review has been posted and answered, which takes the deliberate reopen in step 7; when it does, the section is rebuilt from that new review, so carry across by hand any decision the new review does not re-raise. The `<!-- speckit:confirmed-decisions -->` marker is a tooling hint, not a fence.

The new section is structured as:

```markdown
## Confirmed decisions

<!-- speckit:confirmed-decisions -->

### Requirements    <!-- omit this sub-heading entirely if no requirements decisions -->

- **<title>:** <decision>.
- **<title>:** <decision>.

### Technical    <!-- omit this sub-heading entirely if no technical decisions -->

- **<title>:** <decision>.
- **<title>:** <decision>.
```

Routing rules:

- Bullets corresponding to gaps under `### Requirements gaps` in the review comment go under `### Requirements` here.
- Bullets corresponding to gaps under `### Technical gaps` in the review comment go under `### Technical` here.
- If one group has no decisions, **omit its sub-heading entirely** — do not emit an empty `### Technical` section.
- Strip the gap numbers from the review comment (1, 2, 3, …) when emitting confirmed decisions — bullets here are unordered.
- **Backwards compatibility:** if the review comment has no `### Requirements gaps` / `### Technical gaps` sub-headings (i.e. it was authored before this convention), emit all decisions as a flat bullet list with no sub-headings (the pre-split format).

Preserve everything above the section byte-for-byte. Do not "tidy" the original proposal.

### 5. Patch the issue body

**Link rules** — when extracting `**Recommendation:**` and answer text into bullets, preserve any GitHub URLs verbatim. If you find yourself authoring a *new* link (rare — only if you decompose a multi-part recommendation and need to re-cite a path), use an absolute GitHub URL: `https://github.com/<owner>/<repo>/blob/<default-branch>/<path>` (or `tree/...` for directories, `#L<n>` for line ranges). Never introduce a relative path — the same authored bullet may end up quoted in PR descriptions, comments, or third-party renderers where relative paths break. Resolve `<owner>/<repo>` from step 1's `gh issue view` invocation; resolve `<default-branch>` via `gh repo view <owner>/<repo> --json defaultBranchRef --jq .defaultBranchRef.name`.

Write the new body to `.claude/scratch/speckit-confirmissue-body.md` with the **Write tool**, never via shell heredoc. Run `mkdir -p .claude/scratch` first if the directory does not yet exist (it is git-ignored).

Then use the JSON-via-jq pattern to avoid any shell escaping pitfalls (backticks, `$`, etc.):

```bash
jq -n --rawfile b .claude/scratch/speckit-confirmissue-body.md '{body: $b}' > .claude/scratch/speckit-confirmissue-patch.json
gh api --method PATCH repos/<owner>/<repo>/issues/<number> --input .claude/scratch/speckit-confirmissue-patch.json --jq .html_url
```

(Omit the leading `/` on the endpoint — Git Bash on Windows rewrites `/repos/...`
as a filesystem path. `gh api` accepts both forms on Linux/macOS.)

**Confirm the patch landed before going on.** The `--jq .html_url` above prints the issue URL on success; a failed PATCH prints an error instead. If the body did not land, **stop here** — do not apply the label and do not delete the review. Steps 6 and 7 both assume the decisions are safely on the issue, and step 7 is irreversible.

### 6. Apply the `ready` label

With the body patched, mark the issue as fully defined:

```bash
gh issue edit <number> --repo <owner/repo> --add-label ready
```

- **Additive only.** `--add-label` adds `ready` and touches nothing else — every label the issue already carries stays on it. Never pass `--remove-label` here; tidying `needs triage` or anything else alongside it is a manual call.
- **Idempotent.** Adding a label an issue already carries is a no-op on GitHub's side, so re-running this command on an already-`ready` issue leaves it `ready` and reports no error.
- **Never fatal.** If the label cannot be applied, do **not** fail the command — the decisions are already saved. Continue to the report and say there that the label did not land.

Ordering matters three ways now: the body patch runs first so a label failure can never leave the decisions unsaved; this step sits downstream of step 2's hard-stops so an issue with an unanswered or hedging gap can never come out labelled `ready`; and step 7's deletion sits downstream of *this* step, so the review is never removed before the label that replaces it as the done-marker is on the issue.

### 7. Delete the review comment

The decisions are on the issue body and the `ready` label is on the issue. The review has nothing left to contribute, so remove it:

```bash
gh api --method DELETE repos/<owner>/<repo>/issues/comments/<comment-id>
```

The comment id is already resolved in step 1, so this needs no extra lookup.

- **Only after step 6 succeeded.** If the `ready` label did not land, **do not delete the comment** — the label is the done-marker, and removing the review without it would leave the issue looking never-reviewed to `/speckit.reviewissue` and to the workflow guard. Keep the comment, and say in the report that it is still there because the label did not apply.
- **Never fatal.** If the deletion itself fails, do **not** fail the command — the decisions are saved and the issue is labelled. Continue to the report and say there that the review is still on the issue.
- **Irreversible and silent.** GitHub sends no notification for a deleted comment and offers no undo. That is why every hard-stop in this command sits upstream of this step: a run that stops at step 1, 2, 5 or 6 leaves the review exactly where it was, answerable and re-runnable.

**Putting a confirmed issue back into review.** Nothing here is a one-way door for the *issue*. If the scope moves and the decisions no longer hold, remove the `ready` label (`gh issue edit <number> --repo <owner/repo> --remove-label ready`) — an issue whose scope has moved is by definition no longer ready — and request a review again. With the marker gone and no review comment on the thread, `/speckit.reviewissue` and the workflow both treat it as a first run and post a fresh review. No one has to hand-edit the issue body to make that happen.

---

## Output to the user

Keep your chat response short:

- confirm the issue updated (number + title)
- state how many decisions were folded in (and the count by pattern: e.g. "8 accepted, 1 with rider, 1 redirected")
- state whether the `ready` label was applied — and if it was not, say so explicitly, noting the decisions were saved regardless
- state whether the review comment was deleted — and if it was not, say so explicitly, naming which reason applies (the label did not land, or the deletion failed) and that the review is still on the issue
- return the issue URL

If you stopped at step 1 or 2, report which precondition failed and what to fix. An issue that is already confirmed is not a failure — report it as already confirmed, with the decision count.

Do **not** restate the decisions list in chat — it lives on the issue.

---

## When NOT to use this command

- The issue has no `/speckit.reviewissue` comment yet — run that first.
- The review comment exists but has empty `> _Answer:_` slots — fill them in first.
- The issue body already has a `## Confirmed decisions` section — a green run deleted the review, so there is nothing left to fold. Re-running is safe (it stops at step 1 and reports the issue as already confirmed) but pointless. To revise a decision, edit the bullet; to redo the review entirely, follow the reopen path in step 7.
- The user wants to skip review and go straight to spec — that is `/speckit.specify` directly, no decision log needed.
