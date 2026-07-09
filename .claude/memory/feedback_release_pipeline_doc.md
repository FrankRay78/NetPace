---
name: Release-pipeline changes must update docs/RELEASING.md
description: Any change to .github/workflows/release-binaries.yml (new RID, new variant, runner change, smoke-test scope, size-assertion shape) must be paired with an update to docs/RELEASING.md in the same PR.
type: feedback
---

When editing `.github/workflows/release-binaries.yml` — adding a new RID, adding/removing a variant, changing a runner, modifying the smoke-test command set, or altering the size-assertion contract — the same PR must update `docs/RELEASING.md` to match. The matrix table, runner-per-RID rationale, naming convention, smoke-test contract, and size-assertion contract all live in `docs/RELEASING.md`.

**Why:** `docs/RELEASING.md` is the single source of truth for "how do releases work?" — it exists so a future contributor can answer questions about the release matrix without opening workflow YAML. If the doc drifts from the workflow, the next person adding a RID (Windows AOT, macOS AOT) will either re-derive the answer from YAML (defeating the point of the doc) or copy stale guidance (worse). The convention was established when `docs/RELEASING.md` was first created in feature 001-linux-aot-release alongside the matrix expansion to 14 archives; locking it in here prevents future drift.

**How to apply:** Whenever a diff touches `.github/workflows/release-binaries.yml` (or any sibling release workflow) in a way that changes externally-observable shape — runner, RID, variant, archive name, smoke-test commands, size-assertion bounds — read `docs/RELEASING.md` first, identify the section that needs to change, and update it in the same PR. Pure plumbing edits (e.g. bumping `actions/checkout@v4` → `@v5`) don't need a doc update; user-visible matrix changes always do.
