# NetPace schedules gaffer's chain

**Intent:** One copy of the chain orchestration, in the plugin whose commands it drives, so the unattended agent's workspace no longer holds the code orchestrating it and the verdict reader and the commands writing it are always one commit.

**Behaviour:** `scripts/systemd/netpace-chain-next.service` fast-forwards the runner's gaffer clone and runs gaffer's `chain/chain-next.sh` against `.gaffer/chain.json`; NetPace's `chain.sh`, `chain-next.sh` and their tests are gone. gaffer's `docs/chain.md` is the reference.

**Supersedes:** [`2026-11-01-netpace-runs-gaffers-commands`](2026-11-01-netpace-runs-gaffers-commands.md) — the chain as a NetPace script loading gaffer from a clone, and the constraint that a verdict-schema bump lands in NetPace's chain first; both now live in one gaffer commit. [`2026-10-29-one-table-for-stage-models`](2026-10-29-one-table-for-stage-models.md) — the table as the one place every unattended stage's model is written: it now binds the CI workflows only, and the chain's three stages are the `stages` key of the settings file, read by gaffer's chain, with the same rule that a hand run keeps the session's model.

**Constraints:**

- The chain's three stages left `scripts/stage-models.tsv`; their model and effort are the `stages` key of `.gaffer/chain.json`. The table still binds the CI workflows.
- Updating the gaffer clone is the unit's `ExecStartPre`, not the runner's: the runner refuses an edited or diverged checkout and never changes it.

**Decisions:**

- Rejected keeping the settings on the machine: committed, a change to the writers or the permissions opt-in is reviewed. The runner reads the file from its clone as the previous firing left it — `main` as of then — so a change takes effect one firing after it merges, and the clone must hold the file before the first firing (the unit header says how).
- Rejected a NetPace test of the settings file: the tool that reads it is gaffer's runner, which refuses a key it does not know, a wrongly shaped value, and a missing clone or writers list; a stage left out falls back to gaffer's default by design, as a hand run falls back to the session's model.

**Date:** 2026-11-01
