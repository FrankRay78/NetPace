# The verdict schema

Every stage command in the build chain — `/build`, `/verify` and `/raise-pr` — ends its report with a verdict: one JSON object, the last thing in the report, saying whether the stage succeeded and what it produced. The human-readable report sits above it. The verdict is the contract between the commands and whatever drives them — today [`scripts/chain.sh`](../scripts/chain.sh), which starts the next stage only on a success verdict and stops on anything else. This file is the one place the schema is stated: the commands show the verdict they write and the workflow docs say how it is read, and both point here for the fields.

## Version 1

```json
{"schema": 1, "outcome": "<outcome>", ...payload}
```

| Field | Type | Present | Meaning |
|---|---|---|---|
| `schema` | number | always | The version of this schema the verdict follows. `1`. |
| `outcome` | string | always | What the stage concluded: `ready`, `verified`, `raised` or `failed`. Matched exactly, lowercase. |
| `branch` | string | with `ready` and `verified` | The branch the stage left its work on. Non-empty, no whitespace. |
| `pr` | string | with `raised` | The pull request this run opened, as a bare URL: `https://github.com/<owner>/<repo>/pull/<n>`. Not an autolink, not a markdown link, nothing after it. |
| `reason` | string | with `failed` | Why the stage stopped, as one short line of prose. A reader passes it on with outer whitespace stripped and any line break or run of whitespace inside it flattened to one space, so it lands on one line; nothing else is changed. |

Each command has one success outcome, and all three share the failure outcome:

| Command | Success | Failure |
|---|---|---|
| `/build` | `{"schema": 1, "outcome": "ready", "branch": "<branch>"}` | `{"schema": 1, "outcome": "failed", "reason": "<short reason>"}` |
| `/verify` | `{"schema": 1, "outcome": "verified", "branch": "<branch>"}` | `{"schema": 1, "outcome": "failed", "reason": "<short reason>"}` |
| `/raise-pr` | `{"schema": 1, "outcome": "raised", "pr": "<url>"}` | `{"schema": 1, "outcome": "failed", "reason": "<short reason>"}` |

A success verdict is written only for work the run did: `raised` names a pull request this run opened, `ready` and `verified` describe a suite this run saw pass. A success verdict that also carries a `reason` contradicts itself and is not read as a success.

**Fields a reader does not recognise are ignored.** A verdict of version 1 may carry more than the fields above, and a reader of version 1 behaves exactly as it would without them. That is what lets a command add to its verdict — assumptions, deferred findings — without a version bump and without touching anything that reads it.

## How a verdict is read

**Position is the verdict's identity.** The verdict is whatever ends the report, and nothing else in the report is read: the final non-blank line, or — because a model writing JSON will often fence it — the whole body of a code fence that closes the report. A verdict quoted anywhere above that, in a code block, a list, a table or a sentence, cannot be mistaken for the stage's own, because a quotation cannot fake being last. A report that ends on anything else — a closing sentence, a note after the verdict — has no readable verdict, however correct the verdict above it was.

**Decoration around the object is tolerated.** The verdict is written by a model, and asking for a bare line does not always get one, so a reader strips one leading heading, blockquote, bullet or numbered-item marker (one, not a stack), a run of `*`, `_` or backticks wrapping the line, an indent of up to three spaces, and the code fence around it when that fence closes the report. Four spaces or a tab is markdown's indented code block — a quotation — and is not stripped. Anything beyond that leaves the text something other than one JSON object, which is no verdict.

**What a reader does with it**, in this order:

1. The text is not one JSON object — prose, malformed JSON, two objects, an array, the retired text verdict (`READY branch=…`), or an empty report: the stage has **no readable verdict**, and the reader says so rather than guessing.
2. The object has no `schema`: no readable verdict, naming the missing version.
3. The `schema` is a version the reader does not know: the stage fails with a reason **naming that version**. Nothing else in the verdict is read, not even a reason — a reader cannot know what the fields of a version it does not understand mean.
4. The object has no `outcome`, or one that is not a string: no readable verdict.
5. The `outcome` is `failed`: the stage failed, with its `reason`. An empty or missing reason is reported as a failure with no reason, never as a dangling dash.
6. The `outcome` is not the one this stage succeeds with: no readable verdict, naming the outcome found. Another stage's success, `unverified`, `Ready` — none is this stage's success.
7. The success outcome also carries a `reason`: no readable verdict — the verdict contradicts itself.
8. The success outcome's payload is missing, empty or the wrong shape — a `branch` with whitespace, a `pr` that is not a bare pull request URL: no readable verdict, naming the missing payload.
9. Otherwise the stage succeeded, with its payload.

The two failure directions are not equal in cost, and the rules above fail safe in the cheap one: a false stop costs the rest of one run, a false success opens a pull request off unverified work.

## Changing the schema

- **Adding a field** needs no version bump. Document it in the table above; readers ignore it until they are taught it.
- **A change a version-1 reader would misread** — renaming or removing a field, changing an outcome word, changing what the payload means — is version 2. Bump the number here and in every reader first, because a reader stops loudly on a version it does not know; then change the writers. `scripts/chain.sh` is the reader today, and the three command prompts are the writers.
