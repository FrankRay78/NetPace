# Quickstart: validating the SDLC command chain

How to prove the chain works, from cheapest to most expensive. Interface details: [contracts/chain-cli.md](contracts/chain-cli.md). Stage outcomes: [data-model.md](data-model.md).

## Prerequisites

- The WSL agent sandbox (or any Linux shell) with `git`, `claude`, `gh`, `jq` and `timeout` on PATH.
- `claude` and `gh` both signed in.
- A clone of this repository.

## 1. The test matrix — free, seconds

```bash
scripts/chain.tests.sh
```

Runs the chain against a throwaway repo with stubbed `claude` and `gh`. **Expect**: every case `ok`, a final `RESULT: <n> passed, 0 failed`, exit code 0. No model is called and your checkout is untouched.

## 2. Report mode — free

```bash
scripts/chain.sh --dry-run 270; echo "exit=$?"
git status --porcelain; git branch --show-current
```

**Expect**: the five stages listed in order, `exit=0`, and afterwards the same branch and the same (empty) status as before.

## 3. Refusals — free

From a feature branch:

```bash
git checkout -b scratch/chain-refusal
scripts/chain.sh 270; echo "exit=$?"
git checkout main && git branch -D scratch/chain-refusal
```

With a dirty tree:

```bash
touch chain-refusal.tmp
scripts/chain.sh 270; echo "exit=$?"
rm chain-refusal.tmp
```

**Expect** each time: a `chain: refused — …` line naming the precondition, `exit=2`, and no new `claude` session in `claude --resume`'s list.

## 4. A full run — expensive (the better part of an hour of model time)

Pick a small, `ready` issue. From a clean, up-to-date `main`:

```bash
scripts/chain.sh <issue>; echo "exit=$?"
```

**Expect**: five `ok` lines in order, a final `chain: done — <pull request URL>`, `exit=0`, and:

- the pull request exists and is the only thing pushed (`git ls-remote --heads origin` shows one new branch);
- `git status --porcelain` is empty;
- if either study pass recorded anything, `docs/study/<issue>.md` is committed on the branch and the second pass only added to it;
- nothing prompted you at any point.

## 5. A forced failure — cheap-ish (one short model session)

Give `/build` too little time so the run stops at the first stage:

```bash
CHAIN_TIMEOUT_BUILD=60 scripts/chain.sh <issue>; echo "exit=$?"
```

**Expect**: `chain: FAILED at [1/5] build — stalled …`, `exit=1`, no later stage started, and the branch (if `/build` created one) left exactly as it was. Then:

```bash
claude --resume
```

**Expect**: the stalled `/build` session is the most recent one for this repo and opens.

Clean up the branch the forced failure left behind by hand once you have looked at it — the chain never does.
