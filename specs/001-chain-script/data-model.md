# Data Model: SDLC Command Chain

Phase 1 output for [plan.md](plan.md). The chain holds no persistent data; everything below lives in shell variables for one run and is gone when the script exits.

## Stage (fixed definition)

| # | Name | Command sent | Session | Success recognised by | Default limit | Limit override |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | build | `/build <N>` | fresh | `READY branch=<branch>` | 2h | `CHAIN_TIMEOUT_BUILD` |
| 2 | study (after build) | `/study <N>` | resumes stage 1 | `STUDIED issue=<N> rows=<n>` | 30m | `CHAIN_TIMEOUT_STUDY` |
| 3 | verify | `/verify` | fresh | `VERIFIED branch=<branch>` | 90m | `CHAIN_TIMEOUT_VERIFY` |
| 4 | study (after verify) | `/study <N>` | resumes stage 3 | `STUDIED issue=<N> rows=<n>` | 30m | `CHAIN_TIMEOUT_STUDY` |
| 5 | raise-pr | `/raise-pr <N>` | fresh | `https://github.com/<owner>/<repo>/pull/<n>` | 30m | `CHAIN_TIMEOUT_RAISE_PR` |

Verdict tokens and why each is recognised the way it is: [research.md R2](research.md#r2--reading-a-stages-verdict).

## Chain run (per invocation)

| Field | Set by | Used by |
| --- | --- | --- |
| `issue` | argument, normalised to digits | every command sent; study-record path |
| `dry_run` | `--dry-run` flag | skips everything after argument validation |
| `repo` (`owner/repo`) | preconditions, from `gh` | stage 5 success URL |
| `branch` | stage 1's `READY branch=` | HEAD checks after stages 2–4; remote check before stage 5 |
| `build_session` | stage 1's reply | stage 2's resume |
| `verify_session` | stage 3's reply | stage 4's resume |
| `record_before` | contents of the issue's study record just before stage 4 (empty if absent) | loss check after stage 4 |
| `pr_url` | stage 5's reply | closing success message |

Session ids are never printed or written (spec FR-018).

## Stage outcome

Exactly one per stage, evaluated in this order — the first that applies wins:

1. `stalled` — the stage exceeded its limit.
2. `process error` — `claude` exited non-zero for any other reason, or its output is not JSON.
3. `session error` — `is_error` is true or `subtype` is not `success`.
4. `reported failure` — the report contains `FAILED reason=<…>`; the reason is carried.
5. `no verdict` — the report contains no success token for this stage.
6. `post-check failure` — the verdict was success but a between-stage check failed: dirty tree, wrong branch, study-record lines lost, or (before stage 5) the branch already on the remote or the remote could not be checked.
7. `success`.

Every outcome except `success` ends the run with exit code 1.

## Run state transitions

```text
start
  ├─ usage error / precondition failed ─────────────────► REFUSED   (exit 2)
  ├─ --dry-run ─► print stages ─────────────────────────► REPORTED  (exit 0)
  └─ stage 1 ─► stage 2 ─► stage 3 ─► stage 4 ─► stage 5 ► RAISED    (exit 0)
        │          │          │          │          │
        └──────────┴──────────┴──────────┴──────────┴───► STOPPED at stage k (exit 1)
```

`REFUSED` and `REPORTED` start no model session. `STOPPED` leaves the branch and tree exactly as stage *k* left them.

## Study record (read, never written)

- **Path**: `docs/study/<N>.md` in the checkout.
- **Invariant checked**: every line present before stage 4 is still present after it. Additions are expected; the count of `rows=` in the verdict is shown but not cross-checked.
