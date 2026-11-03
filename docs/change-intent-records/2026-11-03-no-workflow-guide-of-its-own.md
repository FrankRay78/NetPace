# NetPace keeps no workflow guide of its own

**Intent:** What NetPace adds to gaffer's workflow is documented beside the file it concerns — a workflow, script, hook or unit file header, `CONVENTIONS.md` or a record — not in a separate guide, so no page restates what those files already say and drifts from them.

**Behaviour:** `docs/agentic-workflow-NetPace.md` and the WSL sandbox guide are removed. The issue-label states live in the header of `.github/workflows/reviewissue.yml`, the chain's operating commands in `scripts/systemd/netpace-chain-next.service`, which checks block the merge in `CONVENTIONS.md` → *Pull requests*.

**Decisions:**

- Rejected trimming the guide further: it was written as a delta to sections of gaffer's workflow guide that no longer exist, and most of what remained restated `CONVENTIONS.md`, the records and the script headers.
- Rejected moving the label states into `CONVENTIONS.md`: the commands read that file as instructions, and the label table is for the person applying labels.

**Date:** 2026-11-03
