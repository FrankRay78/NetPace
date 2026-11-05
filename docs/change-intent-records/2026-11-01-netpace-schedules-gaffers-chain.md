# NetPace schedules gaffer's chain

**Intent:** One copy of the chain orchestration, in the plugin whose commands it drives, so the unattended agent's workspace no longer holds the code orchestrating it and the verdict reader and the commands writing it are always one commit.

**Behaviour:** `scripts/systemd/netpace-chain-next.service` fast-forwards the runner's gaffer clone and runs gaffer's `chain/chain-next.sh` against `.gaffer/chain.json`; NetPace's `chain.sh`, `chain-next.sh` and their tests are gone. gaffer's `docs/chain.md` is the reference.

**Supersedes:** [`2026-11-01-netpace-runs-gaffers-commands`](2026-11-01-netpace-runs-gaffers-commands.md) — the chain as a NetPace script loading gaffer from a clone, and the constraint that a verdict-schema bump lands in NetPace's chain first; both now live in one gaffer commit. [`2026-10-29-one-table-for-stage-models`](2026-10-29-one-table-for-stage-models.md) — the table as the one place every unattended stage's model is written: it now binds the CI workflows only, and the chain's three stages are the `stages` key of the settings file, read by gaffer's chain, with the same rule that a hand run keeps the session's model.

**Constraints:**

- The chain's three stages left `scripts/stage-models.tsv`; their model and effort are the `stages` key of `.gaffer/chain.json`. The table still binds the CI workflows.
- Updating the gaffer clone is the unit's `ExecStartPre`, not the runner's: the runner refuses an edited or diverged checkout and never changes it.
- `skip_permissions: true` is set. Of gaffer's conditions for that opt-in, NetPace meets the dedicated clone and the machine set aside for the runner; the `gh` and `claude` sign-ins on that machine are the maintainer's own, not credentials scoped to what the chain pushes and comments on, which is the residual exposure.

**Decisions:**

- Rejected keeping the settings on the machine: committed, a change to the writers or the permissions opt-in reaches `main` through a reviewed pull request. The runner reads the file from its clone as the last building firing left it — that issue's feature branch, or its `attempt/` branch after a park, since a firing that finds nothing eligible resets nothing — so a merged change reaches selection only after a later building firing has reset the clone, and the clone must hold the file before the first firing (the unit header says how).
- Rejected a NetPace test of the settings file: the tool that reads it is gaffer's runner, which refuses an unknown top-level key, an unknown `labels` key or stage name, a wrongly shaped value, and a missing clone or writers list; a stage left out, or a misspelt `model` or `effort` key inside one, falls back to gaffer's default by design, as a hand run falls back to the session's model.

**Date:** 2026-11-01
