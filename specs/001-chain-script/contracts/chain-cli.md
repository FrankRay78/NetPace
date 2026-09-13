# Contract: `scripts/chain.sh` command line

## Synopsis

```text
scripts/chain.sh [--dry-run] <issue>
```

- `<issue>` — a GitHub issue number in this repository, bare (`270`) or hashed (`#270`). Exactly one.
- `--dry-run` — list the stages that would run for `<issue>`, in order, and exit. Runs no git or `claude` command.
- Any other argument shape prints usage and exits 1.

The chain acts on the git repository of the current working directory.

## Environment

| Variable | Default | Effect |
| --- | --- | --- |
| `CHAIN_MODEL` | `claude-opus-5` | Model passed to every stage. |
| `CHAIN_STAGE_TIMEOUT` | unset (per-stage limits: build 2h, study 30m, verify 90m, raise-pr 30m) | Seconds; when set, replaces every stage's limit. |

## Preconditions (checked before any stage)

1. Exactly one valid `<issue>` argument.
2. The working tree is clean.
3. `main` is checked out.

## Output

Chain-authored lines start with `chain:`. Each stage's own report is printed as the stage ends.

```text
chain: [1/5] build — starting
<the /build report>
chain: [1/5] build — ok
…
chain: done — https://github.com/<owner>/<repo>/pull/<n>
```

On failure:

```text
chain: FAILED at [3/5] verify — <reason>
chain: no later stage ran; reopen the failed session with `claude --resume` (most recent headless session in this repo).
```

Refusal:

```text
chain: refused — working tree is not clean; no stage was started.
```

Dry run:

```text
chain: dry run for issue #270 — nothing will be run
  1. build     /build 270
  2. study     /study 270   (resumes build's session)
  3. verify    /verify
  4. study     /study 270   (resumes verify's session)
  5. raise-pr  /raise-pr 270
```

Wording is illustrative; the contract is the content each message carries.

## Exit codes

| Code | Meaning |
| --- | --- |
| `0` | Pull request raised, or dry run printed. |
| `1` | Anything else — refused, a stage failed, or bad arguments. |

## Guarantees

- No stage starts unless every earlier stage succeeded.
- The chain itself runs only read-only git commands; it never resets, cleans, stashes, checks out, deletes, pushes or force-updates.
- Nothing is written to disk by the chain.
