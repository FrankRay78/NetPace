# Contract: `scripts/chain.sh` command line

The chain's only interface is its command line, its environment, its output and its exit code.

## Synopsis

```text
scripts/chain.sh [--dry-run] <issue>
scripts/chain.sh --help
```

- `<issue>` — a GitHub issue number in this repository, bare (`270`) or hashed (`#270`). Exactly one.
- `--dry-run` — list the stages that would run for `<issue>`, in order, and exit. Runs no git, `gh` or `claude` command.
- `--help` / `-h` — print usage, the prerequisites and the environment variables below, and exit 0.

The chain acts on the git repository of the **current working directory**, not the directory the script lives in.

## Environment

| Variable | Default | Effect |
| --- | --- | --- |
| `CHAIN_MODEL` | `claude-opus-5` | Model passed to every stage. |
| `CHAIN_TIMEOUT_BUILD` | `7200` | Seconds before `/build` is treated as stalled. |
| `CHAIN_TIMEOUT_STUDY` | `1800` | Seconds before either study pass is treated as stalled. |
| `CHAIN_TIMEOUT_VERIFY` | `5400` | Seconds before `/verify` is treated as stalled. |
| `CHAIN_TIMEOUT_RAISE_PR` | `1800` | Seconds before `/raise-pr` is treated as stalled. |

A timeout that is not a positive integer is a usage error.

## Prerequisites (checked before any stage, in this order)

1. Exactly one valid `<issue>` argument.
2. `git`, `claude`, `gh`, `jq`, `timeout` on PATH.
3. The working directory is inside a git work tree.
4. `gh` is signed in, and the repository resolves to `<owner>/<repo>`.
5. The working tree is clean.
6. `main` is checked out.
7. `origin/main` can be fetched.
8. Local `main` has no commits that `origin/main` lacks.

## Output

All chain-authored lines start with `chain:`. Each stage's own final report is printed, unprefixed, as the stage ends.

```text
chain: issue #270 — 5 stages, model claude-opus-5
chain: [1/5] build — starting
<the /build final report>
chain: [1/5] build — ok (branch feature/270-chain-script)
…
chain: [5/5] raise-pr — ok
chain: done — https://github.com/<owner>/<repo>/pull/<n>
```

On failure the last line names the stage, its position and the reason, and is followed by recovery guidance:

```text
chain: FAILED at [3/5] verify — reported failure: <reason from the stage>
chain: nothing after verify ran; the branch is as verify left it.
chain: reopen that session with `claude --resume` (it is the most recent headless session in this repo).
```

Refusals name the failed precondition and state that no stage was started:

```text
chain: refused — working tree is not clean; no stage was started.
```

Dry run:

```text
chain: dry run for issue #270 — nothing will be run or changed
  1. build      /build 270
  2. study      /study 270    (resumes build's session)
  3. verify     /verify
  4. study      /study 270    (resumes verify's session)
  5. raise-pr   /raise-pr 270
```

Exact wording is illustrative; the contract is the content each message carries.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Pull request raised, or dry run printed, or help printed. |
| `1` | A stage failed; the run stopped partway. |
| `2` | Usage error or precondition failure; no stage was started. |

## Guarantees

- No stage starts unless every earlier stage succeeded and passed its between-stage checks.
- The chain itself never runs `git reset`, `git clean`, `git stash`, `git checkout`, `git branch -d/-D`, `git push`, or any other command that changes the working tree, branches or remote. The only write it performs is `git fetch origin main` during preconditions.
- The only outward-facing action in a run is the pull request opened by `/raise-pr`.
- Nothing is written to disk by the chain: no logs, no state files, no session ids.
