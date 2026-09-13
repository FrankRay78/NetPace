# Quickstart: validating the SDLC command chain

From cheapest to most expensive. Interface: [contracts/chain-cli.md](contracts/chain-cli.md).

## Prerequisites

- The WSL agent sandbox (or any Linux shell) with `git`, `claude`, `gh`, `jq` and `timeout` on PATH; `claude` and `gh` signed in.

## 1. The test matrix — free, seconds

```bash
scripts/chain.tests.sh
```

**Expect**: every case `ok`, `RESULT: <n> passed, 0 failed`, exit 0. No model is called; your checkout is untouched.

## 2. Report mode — free

```bash
scripts/chain.sh --dry-run 270; echo "exit=$?"
```

**Expect**: the five stages in order, `exit=0`, and `git status` unchanged.

## 3. Refusals — free

```bash
touch chain-refusal.tmp
scripts/chain.sh 270; echo "exit=$?"
rm chain-refusal.tmp
```

**Expect**: a `chain: refused — …` line naming the dirty tree, `exit=1`, no stage started. Repeat from a feature branch for the not-on-`main` refusal.

## 4. A full run — expensive (the better part of an hour of model time)

From a clean `main`, against a small `ready` issue:

```bash
scripts/chain.sh <issue>; echo "exit=$?"
```

**Expect**: five `ok` lines in order, `chain: done — <pull request URL>`, `exit=0`, no prompt at any point, and a clean working tree.

## 5. A forced failure and reopening its session — cheap

```bash
CHAIN_STAGE_TIMEOUT=60 scripts/chain.sh <issue>; echo "exit=$?"
claude --resume
```

**Expect**: `chain: FAILED at [1/5] build — stalled`, `exit=1`, no later stage started; in `claude --resume`, the stalled `/build` session is the most recent headless one for this repo and opens. Clean up any branch it left by hand.
