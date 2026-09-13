# Data Model: SDLC Command Chain

Phase 1 output for [plan.md](plan.md). The chain holds no persistent data; everything below lives in shell variables for one run.

## Stage (fixed definition)

| # | Name | Command sent | Session | Success recognised by | Limit |
| --- | --- | --- | --- | --- | --- |
| 1 | build | `/build <N>` | fresh | `READY branch=` | 2h |
| 2 | study | `/study <N>` | resumes stage 1 | `STUDIED issue=` | 30m |
| 3 | verify | `/verify` | fresh | `VERIFIED branch=` | 90m |
| 4 | study | `/study <N>` | resumes stage 3 | `STUDIED issue=` | 30m |
| 5 | raise-pr | `/raise-pr <N>` | fresh | a `https://github.com/…/pull/<n>` URL | 30m |

`CHAIN_STAGE_TIMEOUT` replaces every limit when set.

## Chain run (per invocation)

| Field | Set by | Used by |
| --- | --- | --- |
| `issue` | argument, normalised to digits | every command sent |
| `dry_run` | `--dry-run` flag | skips everything after argument validation |
| `build_session` | stage 1's reply | stage 2's resume |
| `verify_session` | stage 3's reply | stage 4's resume |

Session ids are never printed or written.

## Stage outcome

- **Failed** — `claude` exited non-zero (a timeout is reported as "stalled"), or the report has no success verdict for this stage, or it has a `FAILED reason=` verdict. The reason shown is the stage's own `FAILED reason=` when present, otherwise "stalled", "exited with <code>" or "no recognisable verdict".
- **Succeeded** — otherwise.

## Run state transitions

```text
start
  ├─ bad argument / dirty tree / not on main ───────────► REFUSED   (exit 1)
  ├─ --dry-run ─► print stages ─────────────────────────► REPORTED  (exit 0)
  └─ stage 1 ─► stage 2 ─► stage 3 ─► stage 4 ─► stage 5 ► RAISED    (exit 0)
        │          │          │          │          │
        └──────────┴──────────┴──────────┴──────────┴───► STOPPED at stage k (exit 1)
```

`REFUSED` and `REPORTED` start no stage.
