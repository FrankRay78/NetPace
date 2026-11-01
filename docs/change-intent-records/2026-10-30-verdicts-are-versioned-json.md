# Stage verdicts are versioned JSON objects

**Intent:** Make the verdict that `/build`, `/verify` and `/raise-pr` end on an explicit, versioned contract stated once, so it can cross a repository boundary when the commands become a plugin, and so fields can be added without breaking what reads it. Refs #476.

**Behaviour:** gaffer's [`docs/verdict-schema.md`](https://github.com/FrankRay78/gaffer-claude/blob/main/docs/verdict-schema.md), where it moved with the commands; read by [`scripts/chain.sh`](../../scripts/chain.sh).

**Constraints:** Position stays the verdict's identity ([2026-10-09](2026-10-09-verdict-read-by-position.md) is not overturned): only the end of the report is read, and the old text verdict is not accepted alongside the new one.

**Decisions:**

- A `schema` number and an `outcome` word, with the payload in `branch`, `pr` or `reason`. Rejected: a `command` field — the outcome word already names the stage, and a field no reader checks is a field that drifts.
- A code fence that closes the report is read as holding the verdict, reversing the earlier "a fenced verdict is not a verdict". Rejected: keeping the ban — a model writing JSON fences it more often than not, and a fence at the end of the report moves the verdict nowhere; a quotation still cannot fake being last.
- A success verdict carrying a `reason` is refused. Rejected: ignoring it as an unknown field — it is the JSON shape of the one-line hedge that once passed the text gate, and refusing it costs nothing a correct writer would send.
- The schema in its own file, not in the workflow guide. Rejected: a section of `agentic-workflow.md` — the file moves with the commands when they are extracted; the guide does not.
- Bare multi-line JSON is not read; only a fence extends the verdict past one line. Rejected: walking back to an opening brace — a tolerance with no observed need, pinned as unreadable so the limit is known.

**Date:** 2026-10-30
