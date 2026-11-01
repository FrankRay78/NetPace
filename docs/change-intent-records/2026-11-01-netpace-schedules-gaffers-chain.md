# NetPace schedules gaffer's chain

**Intent:** One copy of the chain orchestration, in the plugin whose commands it drives, so the unattended agent's workspace no longer holds the code orchestrating it and the verdict reader and the commands writing it are always one commit.

**Behaviour:** `scripts/systemd/netpace-chain-next.service` fast-forwards the runner's gaffer clone and runs gaffer's `chain/chain-next.sh` against `.gaffer/chain.json`; NetPace's `chain.sh`, `chain-next.sh` and their tests are gone. gaffer's `docs/chain.md` is the reference.

**Supersedes:** [`2026-11-01-netpace-runs-gaffers-commands`](2026-11-01-netpace-runs-gaffers-commands.md) — the chain as a NetPace script loading gaffer from a clone, and the constraint that a verdict-schema bump lands in NetPace's chain first; both now live in one gaffer commit.

**Constraints:**

- The chain's three stages left `scripts/stage-models.tsv`; their model and effort are the `stages` key of `.gaffer/chain.json`. The table still binds the CI workflows.
- Updating the gaffer clone is the unit's `ExecStartPre`, not the runner's: the runner refuses an edited or diverged checkout and never changes it.

**Decisions:**

- Rejected keeping the settings on the machine: committed, a change to the writers or the permissions opt-in is reviewed, and it travels with the clone the runner resets.
- Rejected a NetPace test of the settings file: the tool that reads it is gaffer's runner, whose matrix covers every key, and a hand-rolled check would cover less.

**Date:** 2026-11-01
